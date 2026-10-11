using SolarUY.SensorSimulator.Configuration;
using SolarUY.SensorSimulator.Modelos;

namespace SolarUY.SensorSimulator.Simulacion;

/// <summary>
/// Genera lecturas creíbles para un panel (sección 7 de la spec).
/// Es lógica pura: recibe la hora como parámetro y un Random inyectable, así se puede probar sin depender del reloj.
/// </summary>
public sealed class GeneradorLectura
{
    public const double TemperaturaAmbienteC = 20;

    private readonly SimuladorOptions _options;
    private readonly Random _random;
    private readonly TimeZoneInfo _zona;

    public GeneradorLectura(SimuladorOptions options, Random? random = null)
    {
        _options = options;
        _random = random ?? Random.Shared; // Random.Shared es thread-safe (llegan requests en paralelo)
        _zona = ZonaHoraria.Resolver(options.ZonaHoraria);
    }

    /// <summary>
    /// 0 de noche, 1 al mediodía solar: max(0, sin(π · (horaLocal − 6) / 12)).
    /// Entre las 6:00 y las 18:00 es una campana; fuera de ese rango el seno es negativo y se corta en 0.
    /// </summary>
    public double FactorSolar(DateTime ahoraUtc)
    {
        var utc = DateTime.SpecifyKind(ahoraUtc, DateTimeKind.Utc);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, _zona);
        var hora = local.TimeOfDay.TotalHours;
        return Math.Max(0.0, Math.Sin(Math.PI * (hora - 6) / 12));
    }

    /// <summary>
    /// Actualiza el estado de falla del panel: limpia las fallas aleatorias vencidas y,
    /// con probabilidad ProbabilidadFalla, hace entrar en falla a un panel sano y activo.
    /// Las fallas forzadas (sin vencimiento) nunca se limpian solas.
    /// </summary>
    public void ActualizarFalla(PanelSimulado panel, DateTime ahoraUtc)
    {
        if (panel.Falla is { ExpiraUtc: { } expira } && ahoraUtc >= expira)
        {
            panel.Falla = null;
        }

        if (panel.Falla is null
            && panel.Activo
            && _options.ProbabilidadFalla > 0
            && _random.NextDouble() < _options.ProbabilidadFalla)
        {
            var tipo = _random.Next(2) == 0 ? TipoFalla.Sobretemperatura : TipoFalla.CaidaPotencia;
            panel.Falla = new FallaActiva(tipo, ahoraUtc.AddSeconds(_options.DuracionFallaSegundos));
        }
    }

    public LecturaPanelDto Generar(PanelSimulado panel, DateTime ahoraUtc)
    {
        if (!panel.Activo)
        {
            // Panel desactivado: sin corriente, tensión ni potencia; queda a temperatura ambiente.
            return new LecturaPanelDto(panel.Codigo, false, 0, 0, 0, Redondear(TemperaturaAmbienteC + Ruido(1)));
        }

        var factor = FactorSolar(ahoraUtc);
        var potencia = panel.PotenciaNominalW * factor * (1 + Ruido(0.05));   // ruido ±5 %
        var voltaje = 30 + _random.NextDouble() * 2;                          // 30–32 V
        var temperatura = TemperaturaAmbienteC + 30 * factor + Ruido(1.5);    // correlacionada con la potencia

        switch (panel.Falla?.Tipo)
        {
            case TipoFalla.Sobretemperatura:
                // Siempre por encima de 75 °C, aun de noche, para que la falla forzada sea demostrable.
                temperatura = Math.Max(temperatura, 80) + _random.NextDouble() * 5;
                break;
            case TipoFalla.CaidaPotencia:
                potencia *= 0.5;
                break;
        }

        var corriente = potencia / voltaje;

        return new LecturaPanelDto(
            panel.Codigo,
            true,
            Redondear(corriente),
            Redondear(voltaje),
            Redondear(potencia),
            Redondear(temperatura));
    }

    /// <summary>Número aleatorio uniforme en [-amplitud, +amplitud).</summary>
    private double Ruido(double amplitud) => (_random.NextDouble() * 2 - 1) * amplitud;

    private static double Redondear(double valor) => Math.Round(valor, 2);
}
