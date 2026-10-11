using Microsoft.Extensions.Options;
using SolarUY.SensorSimulator.Configuration;
using SolarUY.SensorSimulator.Endpoints;
using SolarUY.SensorSimulator.Simulacion;

var builder = WebApplication.CreateBuilder(args);

// Las opciones se leen de la sección "Simulador". Se resuelven de forma perezosa (al primer uso),
// cuando la configuración ya está completa (appsettings + variables de entorno).
builder.Services.Configure<SimuladorOptions>(builder.Configuration.GetSection(SimuladorOptions.Seccion));

// El reloj se inyecta para poder fijar la hora en los tests.
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddSingleton(sp =>
    new GeneradorLectura(sp.GetRequiredService<IOptions<SimuladorOptions>>().Value));

builder.Services.AddSingleton(sp =>
{
    var registro = new SensorStore();
    registro.CargarIniciales(sp.GetRequiredService<IOptions<SimuladorOptions>>().Value);
    return registro;
});

var app = builder.Build();

app.MapSensorEndpoints();
app.MapControlEndpoints();

var sensores = app.Services.GetRequiredService<SensorStore>();
app.Logger.LogInformation(
    "Simulador listo con {Cantidad} sensor(es): {Ids}.",
    sensores.Cantidad,
    string.Join(", ", sensores.Listar().Select(s => s.IdentificadorHardwareARM)));

app.Run();

// Hace visible la clase Program para los tests de integración (WebApplicationFactory<Program>).
public partial class Program { }
