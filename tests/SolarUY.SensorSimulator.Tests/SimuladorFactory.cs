using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SolarUY.SensorSimulator.Tests;

/// <summary>
/// Levanta el simulador real en memoria (sin abrir puertos) con:
///  - fallas aleatorias apagadas (tests deterministas),
///  - un reloj fijo en el mediodía de Montevideo (la curva solar da potencia máxima).
/// </summary>
public sealed class SimuladorFactory : WebApplicationFactory<Program>
{
    // 15:00Z = 12:00 en Montevideo (UTC-3)
    public static readonly DateTimeOffset MediodiaUy = new(2026, 10, 10, 15, 0, 0, TimeSpan.Zero);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Simulador:ProbabilidadFalla"] = "0",
                ["Simulador:NodosIniciales"] = "3",
                ["Simulador:PanelesPorNodo"] = "5",
                ["Simulador:PotenciaNominalPanelW"] = "300"
            }));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new RelojFijo(MediodiaUy));
        });
    }

    public HttpClient CrearCliente() => CreateClient();
}

public sealed class RelojFijo : TimeProvider
{
    private readonly DateTimeOffset _ahora;

    public RelojFijo(DateTimeOffset ahora) => _ahora = ahora;

    public override DateTimeOffset GetUtcNow() => _ahora;
}

internal static class HttpExtensiones
{
    public static async Task<JsonElement> LeerJsonAsync(this HttpResponseMessage respuesta) =>
        await respuesta.Content.ReadFromJsonAsync<JsonElement>();

    public static JsonElement Panel(this JsonElement estado, string codigo) =>
        estado.GetProperty("paneles").EnumerateArray()
            .First(p => p.GetProperty("codigoPanel").GetString() == codigo);
}
