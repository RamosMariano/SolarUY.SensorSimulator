namespace SolarUY.SensorSimulator.Simulacion;

/// <summary>
/// Falla en curso. ExpiraUtc nulo significa falla forzada: dura hasta que se la restablece.
/// PorcentajeCaida solo tiene sentido para CaidaPotencia (cuánto baja la potencia, de 0 a 100);
/// si es nulo se usa la caída histórica del simulador: la mitad.
/// </summary>
public sealed record FallaActiva(TipoFalla Tipo, DateTime? ExpiraUtc, double? PorcentajeCaida = null)
{
    /// <summary>Caída de potencia cuando no se indica porcentaje (compatible con el comportamiento anterior).</summary>
    public const double CaidaPotenciaPorDefecto = 50;

    public bool EsForzada => ExpiraUtc is null;

    /// <summary>Porcentaje que realmente se aplica a una caída de potencia.</summary>
    public double PorcentajeCaidaEfectivo => PorcentajeCaida ?? CaidaPotenciaPorDefecto;

    /// <summary>Lo que informa el inventario: el porcentaje si es una caída de potencia, nulo en cualquier otro caso.</summary>
    public double? PorcentajeCaidaInformado =>
        Tipo == TipoFalla.CaidaPotencia ? PorcentajeCaidaEfectivo : null;
}

/// <summary>
/// Estado en memoria de un panel simulado.
/// No es thread-safe por sí solo: SensorSimulado serializa los accesos con un lock.
/// </summary>
public sealed class PanelSimulado
{
    public PanelSimulado(string codigo, double potenciaNominalW)
    {
        Codigo = codigo;
        PotenciaNominalW = potenciaNominalW;
    }

    public string Codigo { get; }
    public double PotenciaNominalW { get; }
    public bool Activo { get; set; } = true;
    public FallaActiva? Falla { get; set; }
}
