using SolarUY.SensorSimulator.Modelos;
using SolarUY.SensorSimulator.Simulacion;

namespace SolarUY.SensorSimulator.Endpoints;

/// <summary>
/// API del sensor: es la que consulta el sistema de gestión (sección 3 de la spec).
/// GET estado + órdenes (activar/desactivar panel, activar/desactivar volcado a la red).
/// </summary>
public static class SensorEndpoints
{
    public static IEndpointRouteBuilder MapSensorEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/sensores");

        grupo.MapGet("/{hw}/estado", ObtenerEstado);

        grupo.MapPost("/{hw}/paneles/{codigo}/activar",
            (string hw, string codigo, SensorStore store, GeneradorLectura generador, TimeProvider reloj) =>
                CambiarPanel(hw, codigo, true, store, generador, reloj));

        grupo.MapPost("/{hw}/paneles/{codigo}/desactivar",
            (string hw, string codigo, SensorStore store, GeneradorLectura generador, TimeProvider reloj) =>
                CambiarPanel(hw, codigo, false, store, generador, reloj));

        grupo.MapPost("/{hw}/volcado/activar",
            (string hw, SensorStore store, GeneradorLectura generador, TimeProvider reloj) =>
                CambiarVolcado(hw, true, store, generador, reloj));

        grupo.MapPost("/{hw}/volcado/desactivar",
            (string hw, SensorStore store, GeneradorLectura generador, TimeProvider reloj) =>
                CambiarVolcado(hw, false, store, generador, reloj));

        return app;
    }

    private static IResult ObtenerEstado(string hw, SensorStore store, GeneradorLectura generador, TimeProvider reloj)
    {
        if (!store.TryObtener(hw, out var sensor))
        {
            return Results.NotFound(new ErrorDto($"El sensor '{hw}' no existe."));
        }

        return Results.Ok(sensor.LeerEstado(generador, reloj.GetUtcNow().UtcDateTime));
    }

    private static IResult CambiarPanel(string hw, string codigo, bool activar, SensorStore store, GeneradorLectura generador, TimeProvider reloj)
    {
        if (!store.TryObtener(hw, out var sensor))
        {
            return Results.NotFound(new ErrorDto($"El sensor '{hw}' no existe."));
        }

        var resultado = sensor.CambiarEstadoPanel(codigo, activar);

        return resultado switch
        {
            ResultadoOrden.Ok => Results.Ok(sensor.LeerEstado(generador, reloj.GetUtcNow().UtcDateTime)),
            ResultadoOrden.PanelInexistente => Results.NotFound(new ErrorDto($"El panel '{codigo}' no existe en el sensor '{hw}'.")),
            _ => Results.Conflict(new ErrorDto($"El panel '{codigo}' ya estaba {(activar ? "activo" : "desactivado")}."))
        };
    }

    private static IResult CambiarVolcado(string hw, bool activar, SensorStore store, GeneradorLectura generador, TimeProvider reloj)
    {
        if (!store.TryObtener(hw, out var sensor))
        {
            return Results.NotFound(new ErrorDto($"El sensor '{hw}' no existe."));
        }

        var resultado = sensor.CambiarVolcado(activar);

        return resultado == ResultadoOrden.Ok
            ? Results.Ok(sensor.LeerEstado(generador, reloj.GetUtcNow().UtcDateTime))
            : Results.Conflict(new ErrorDto($"El volcado a la red ya estaba {(activar ? "activo" : "desactivado")}."));
    }
}
