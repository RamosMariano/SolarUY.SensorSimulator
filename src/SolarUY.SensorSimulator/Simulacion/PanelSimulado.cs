namespace SolarUY.SensorSimulator.Simulacion;

/// <summary>Falla en curso. ExpiraUtc nulo significa falla forzada: dura hasta que se la restablece.</summary>
public sealed record FallaActiva(TipoFalla Tipo, DateTime? ExpiraUtc)
{
    public bool EsForzada => ExpiraUtc is null;
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
