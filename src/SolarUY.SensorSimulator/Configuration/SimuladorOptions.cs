namespace SolarUY.SensorSimulator.Configuration;

/// <summary>
/// Opciones de la sección "Simulador" de appsettings.json.
/// </summary>
public sealed class SimuladorOptions
{
    public const string Seccion = "Simulador";

    /// <summary>Cantidad de sensores (nodos) que se crean al iniciar, si no se define la lista "Nodos".</summary>
    public int NodosIniciales { get; set; } = 3;

    /// <summary>Paneles por sensor cuando se usa NodosIniciales.</summary>
    public int PanelesPorNodo { get; set; } = 5;

    public double PotenciaNominalPanelW { get; set; } = 300;

    /// <summary>Prefijo del identificador de hardware: ARM-001, ARM-002, ...</summary>
    public string PrefijoHardware { get; set; } = "ARM-";

    public string ZonaHoraria { get; set; } = "America/Montevideo";

    /// <summary>Probabilidad (0 a 1) de que un panel sano entre en falla en cada consulta de estado.</summary>
    public double ProbabilidadFalla { get; set; } = 0.01;

    /// <summary>Cuánto dura una falla aleatoria antes de recuperarse sola.</summary>
    public int DuracionFallaSegundos { get; set; } = 120;

    public double EficienciaConversion { get; set; } = 0.97;

    /// <summary>Topología explícita (no uniforme). Si tiene elementos, reemplaza a NodosIniciales y PanelesPorNodo.</summary>
    public List<NodoConfig> Nodos { get; set; } = new();
}

public sealed class NodoConfig
{
    public string Id { get; set; } = string.Empty;
    public int Paneles { get; set; }
    public double? PotenciaNominalW { get; set; }
}
