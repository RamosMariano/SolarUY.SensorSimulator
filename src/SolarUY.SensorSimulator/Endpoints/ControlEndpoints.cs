using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using SolarUY.SensorSimulator.Configuration;
using SolarUY.SensorSimulator.Modelos;
using SolarUY.SensorSimulator.Simulacion;

namespace SolarUY.SensorSimulator.Endpoints;

/// <summary>
/// API de control del simulador (sección 4 de la spec). NO forma parte del contrato real del sensor:
/// sirve para agregar nodos en caliente y forzar fallas durante demos y la defensa.
/// </summary>
public static class ControlEndpoints
{
    private const int MaxPanelesPorSensor = 200;

    // El identificador viaja en la URL, por eso se limita a letras, números, guion y guion bajo.
    private static readonly Regex IdentificadorValido = new("^[A-Za-z0-9_-]{1,50}$", RegexOptions.Compiled);

    public static IEndpointRouteBuilder MapControlEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/simulador");

        grupo.MapGet("/inventario", (SensorStore store) =>
            Results.Ok(store.Listar().Select(s => s.Inventario()).ToList()));

        grupo.MapPost("/sensores", (AltaSensorRequest request, SensorStore store, IOptions<SimuladorOptions> opciones) =>
            AltaSensor(request, store, opciones.Value));

        grupo.MapDelete("/sensores/{hw}", (string hw, SensorStore store) =>
            store.Quitar(hw)
                ? Results.NoContent()
                : Results.NotFound(new ErrorDto($"El sensor '{hw}' no existe.")));

        grupo.MapPost("/fallas/{hw}/{codigo}", (string hw, string codigo, string? tipo, SensorStore store) =>
            ForzarFalla(hw, codigo, tipo, store));

        grupo.MapDelete("/fallas/{hw}/{codigo}", (string hw, string codigo, SensorStore store) =>
            RestablecerFalla(hw, codigo, store));

        return app;
    }

    private static IResult AltaSensor(AltaSensorRequest request, SensorStore store, SimuladorOptions opciones)
    {
        var errores = new Dictionary<string, string[]>();

        var hw = request.IdentificadorHardwareARM?.Trim();
        if (string.IsNullOrEmpty(hw) || !IdentificadorValido.IsMatch(hw))
        {
            errores[nameof(request.IdentificadorHardwareARM)] =
                new[] { "Obligatorio. Hasta 50 caracteres: letras, números, '-' y '_'." };
        }

        if (request.CantidadPaneles < 1 || request.CantidadPaneles > MaxPanelesPorSensor)
        {
            errores[nameof(request.CantidadPaneles)] =
                new[] { $"Debe estar entre 1 y {MaxPanelesPorSensor}." };
        }

        if (request.PotenciaNominalW is <= 0)
        {
            errores[nameof(request.PotenciaNominalW)] =
                new[] { "Debe ser mayor que 0." };
        }

        if (errores.Count > 0)
        {
            return Results.ValidationProblem(errores);
        }

        var potencia = request.PotenciaNominalW ?? opciones.PotenciaNominalPanelW;

        if (!store.Agregar(hw!, request.CantidadPaneles, potencia, opciones.EficienciaConversion))
        {
            return Results.Conflict(new ErrorDto($"Ya existe un sensor '{hw}'."));
        }

        store.TryObtener(hw!, out var sensor);
        return Results.Created($"/sensores/{hw}/estado", sensor!.Inventario());
    }

    private static IResult ForzarFalla(string hw, string codigo, string? tipo, SensorStore store)
    {
        if (!TipoFallaExtensions.TryParseApi(tipo, out var tipoFalla))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["tipo"] = new[] { "Valores válidos: sobretemperatura, caidaPotencia." }
            });
        }

        if (!store.TryObtener(hw, out var sensor))
        {
            return Results.NotFound(new ErrorDto($"El sensor '{hw}' no existe."));
        }

        if (sensor.ForzarFalla(codigo, tipoFalla) != ResultadoOrden.Ok)
        {
            return Results.NotFound(new ErrorDto($"El panel '{codigo}' no existe en el sensor '{hw}'."));
        }

        var panel = sensor.Inventario().Paneles
            .First(p => string.Equals(p.CodigoPanel, codigo, StringComparison.OrdinalIgnoreCase));

        return Results.Ok(panel);
    }

    private static IResult RestablecerFalla(string hw, string codigo, SensorStore store)
    {
        if (!store.TryObtener(hw, out var sensor))
        {
            return Results.NotFound(new ErrorDto($"El sensor '{hw}' no existe."));
        }

        return sensor.RestablecerFalla(codigo) == ResultadoOrden.Ok
            ? Results.NoContent()
            : Results.NotFound(new ErrorDto($"El panel '{codigo}' no existe en el sensor '{hw}'."));
    }
}
