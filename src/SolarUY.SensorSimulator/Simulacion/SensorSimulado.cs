using SolarUY.SensorSimulator.Modelos;

namespace SolarUY.SensorSimulator.Simulacion;

public enum ResultadoOrden
{
    Ok,
    PanelInexistente,
    YaAplicado
}

/// <summary>
/// Un sensor ARM + el inversor de un nodo, con sus paneles.
/// Todo el estado mutable se protege con un lock: ASP.NET Core atiende requests en paralelo.
/// </summary>
public sealed class SensorSimulado
{
    private readonly object _lock = new();
    private readonly List<PanelSimulado> _paneles;
    private bool _volcadoRedActivo = true;

    public SensorSimulado(string identificadorHardwareARM, double eficienciaConversion, IEnumerable<PanelSimulado> paneles)
    {
        IdentificadorHardwareARM = identificadorHardwareARM;
        EficienciaConversion = eficienciaConversion;
        _paneles = paneles.ToList();
    }

    public string IdentificadorHardwareARM { get; }
    public double EficienciaConversion { get; }

    public bool VolcadoRedActivo
    {
        get
        {
            lock (_lock)
            {
                return _volcadoRedActivo;
            }
        }
    }

    /// <summary>Crea un sensor con paneles P01, P02, ... todos de la misma potencia nominal.</summary>
    public static SensorSimulado Crear(string identificadorHardwareARM, int cantidadPaneles, double potenciaNominalW, double eficienciaConversion)
    {
        if (cantidadPaneles < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(cantidadPaneles), "Un sensor necesita al menos un panel.");
        }

        var paneles = Enumerable.Range(1, cantidadPaneles)
            .Select(i => new PanelSimulado($"P{i:D2}", potenciaNominalW));

        return new SensorSimulado(identificadorHardwareARM, eficienciaConversion, paneles);
    }

    public EstadoSensorDto LeerEstado(GeneradorLectura generador, DateTime ahoraUtc)
    {
        lock (_lock)
        {
            var lecturas = new List<LecturaPanelDto>(_paneles.Count);

            foreach (var panel in _paneles)
            {
                generador.ActualizarFalla(panel, ahoraUtc);
                lecturas.Add(generador.Generar(panel, ahoraUtc));
            }

            return new EstadoSensorDto(IdentificadorHardwareARM, ahoraUtc, _volcadoRedActivo, EficienciaConversion, lecturas);
        }
    }

    public ResultadoOrden CambiarEstadoPanel(string codigoPanel, bool activo)
    {
        lock (_lock)
        {
            var panel = Buscar(codigoPanel);
            if (panel is null)
            {
                return ResultadoOrden.PanelInexistente;
            }

            if (panel.Activo == activo)
            {
                return ResultadoOrden.YaAplicado;
            }

            panel.Activo = activo;
            return ResultadoOrden.Ok;
        }
    }

    public ResultadoOrden CambiarVolcado(bool activo)
    {
        lock (_lock)
        {
            if (_volcadoRedActivo == activo)
            {
                return ResultadoOrden.YaAplicado;
            }

            _volcadoRedActivo = activo;
            return ResultadoOrden.Ok;
        }
    }

    /// <summary>Fuerza una falla que dura hasta que se la restablece (útil para demos y defensa).</summary>
    public ResultadoOrden ForzarFalla(string codigoPanel, TipoFalla tipo)
    {
        lock (_lock)
        {
            var panel = Buscar(codigoPanel);
            if (panel is null)
            {
                return ResultadoOrden.PanelInexistente;
            }

            panel.Falla = new FallaActiva(tipo, null);
            return ResultadoOrden.Ok;
        }
    }

    /// <summary>Quita la falla del panel. Es idempotente: si no tenía falla, también devuelve Ok.</summary>
    public ResultadoOrden RestablecerFalla(string codigoPanel)
    {
        lock (_lock)
        {
            var panel = Buscar(codigoPanel);
            if (panel is null)
            {
                return ResultadoOrden.PanelInexistente;
            }

            panel.Falla = null;
            return ResultadoOrden.Ok;
        }
    }

    public InventarioSensorDto Inventario()
    {
        lock (_lock)
        {
            var paneles = _paneles
                .Select(p => new InventarioPanelDto(
                    p.Codigo,
                    p.PotenciaNominalW,
                    p.Activo,
                    p.Falla?.Tipo.ANombreApi(),
                    p.Falla?.EsForzada ?? false))
                .ToList();

            return new InventarioSensorDto(IdentificadorHardwareARM, _volcadoRedActivo, paneles);
        }
    }

    // Debe llamarse con el lock tomado.
    private PanelSimulado? Buscar(string codigoPanel) =>
        _paneles.FirstOrDefault(p => string.Equals(p.Codigo, codigoPanel, StringComparison.OrdinalIgnoreCase));
}
