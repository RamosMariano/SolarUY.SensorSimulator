namespace SolarUY.SensorSimulator.Modelos;

// Contrato HTTP del sensor. Los nombres siguen a LecturaTelemetria del dominio de SolarUY.
// System.Text.Json (defaults de ASP.NET Core) los serializa en camelCase.
// NO se incluye EstadoDetectado: el sensor mide, el sistema de gestión diagnostica.

public sealed record LecturaPanelDto(
    string CodigoPanel,
    bool Activo,
    double CorrienteContinua,
    double Voltaje,
    double PotenciaGeneradaWatts,
    double TemperaturaCelsius);

public sealed record EstadoSensorDto(
    string IdentificadorHardwareARM,
    DateTime FechaHoraUtc,
    bool VolcadoRedActivo,
    double EficienciaConversion,
    IReadOnlyList<LecturaPanelDto> Paneles);

// API de control (solo existe en el simulador)

public sealed record InventarioPanelDto(
    string CodigoPanel,
    double PotenciaNominalW,
    bool Activo,
    string? Falla,
    bool FallaForzada);

public sealed record InventarioSensorDto(
    string IdentificadorHardwareARM,
    bool VolcadoRedActivo,
    IReadOnlyList<InventarioPanelDto> Paneles);

public sealed record AltaSensorRequest(
    string? IdentificadorHardwareARM,
    int CantidadPaneles,
    double? PotenciaNominalW);

public sealed record ErrorDto(string Error);
