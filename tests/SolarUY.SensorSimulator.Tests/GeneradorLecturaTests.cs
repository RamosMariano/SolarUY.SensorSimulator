using SolarUY.SensorSimulator.Configuration;
using SolarUY.SensorSimulator.Simulacion;

namespace SolarUY.SensorSimulator.Tests;

public class GeneradorLecturaTests
{
    // Montevideo es UTC-3: 15:00Z = 12:00 locales (mediodía solar), 03:00Z = 00:00 locales.
    private static readonly DateTime Mediodia = new(2026, 10, 10, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Medianoche = new(2026, 10, 10, 3, 0, 0, DateTimeKind.Utc);

    private static GeneradorLectura Crear(double probabilidadFalla = 0, int duracionFallaSegundos = 120) =>
        new(new SimuladorOptions
            {
                ProbabilidadFalla = probabilidadFalla,
                DuracionFallaSegundos = duracionFallaSegundos,
                ZonaHoraria = "America/Montevideo"
            },
            new Random(1234));

    // ---- Curva solar -------------------------------------------------------------------------

    [Fact]
    public void FactorSolar_es_cero_de_noche()
    {
        Assert.Equal(0.0, Crear().FactorSolar(Medianoche));
    }

    [Fact]
    public void FactorSolar_es_uno_al_mediodia()
    {
        Assert.Equal(1.0, Crear().FactorSolar(Mediodia), 6);
    }

    [Fact]
    public void FactorSolar_es_simetrico_respecto_del_mediodia()
    {
        var generador = Crear();
        var nueveLocal = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);    // 09:00 locales
        var quinceLocal = new DateTime(2026, 10, 10, 18, 0, 0, DateTimeKind.Utc);   // 15:00 locales

        Assert.Equal(generador.FactorSolar(nueveLocal), generador.FactorSolar(quinceLocal), 9);
    }

    // ---- Lecturas ----------------------------------------------------------------------------

    [Fact]
    public void De_noche_potencia_y_corriente_son_cero()
    {
        var lectura = Crear().Generar(new PanelSimulado("P01", 300), Medianoche);

        Assert.True(lectura.Activo);
        Assert.Equal(0.0, lectura.PotenciaGeneradaWatts);
        Assert.Equal(0.0, lectura.CorrienteContinua);
    }

    [Fact]
    public void Al_mediodia_la_potencia_esta_dentro_de_mas_menos_5_por_ciento_de_la_nominal()
    {
        var generador = Crear();
        var panel = new PanelSimulado("P01", 300);

        for (var i = 0; i < 200; i++)
        {
            var potencia = generador.Generar(panel, Mediodia).PotenciaGeneradaWatts;
            Assert.InRange(potencia, 285 - 0.01, 315 + 0.01);
        }
    }

    [Fact]
    public void El_voltaje_esta_entre_30_y_32_voltios()
    {
        var generador = Crear();
        var panel = new PanelSimulado("P01", 300);

        for (var i = 0; i < 200; i++)
        {
            Assert.InRange(generador.Generar(panel, Mediodia).Voltaje, 30.0, 32.0);
        }
    }

    [Fact]
    public void Corriente_por_voltaje_es_aproximadamente_la_potencia()
    {
        var lectura = Crear().Generar(new PanelSimulado("P01", 300), Mediodia);

        // Corriente y voltaje se redondean a 2 decimales por separado, por eso se tolera un pequeño error.
        var diferencia = Math.Abs(lectura.PotenciaGeneradaWatts - lectura.CorrienteContinua * lectura.Voltaje);
        Assert.True(diferencia < 0.5, $"Diferencia inesperada: {diferencia}");
    }

    [Fact]
    public void La_temperatura_esta_correlacionada_con_la_potencia()
    {
        var generador = Crear();
        var panel = new PanelSimulado("P01", 300);

        var dia = generador.Generar(panel, Mediodia).TemperaturaCelsius;
        var noche = generador.Generar(panel, Medianoche).TemperaturaCelsius;

        Assert.InRange(dia, 48.4, 51.6);     // 20 + 30 ± 1,5
        Assert.InRange(noche, 18.4, 21.6);   // 20 ± 1,5
    }

    [Fact]
    public void Un_panel_desactivado_no_genera_nada_y_queda_a_temperatura_ambiente()
    {
        var panel = new PanelSimulado("P01", 300) { Activo = false };

        var lectura = Crear().Generar(panel, Mediodia);

        Assert.False(lectura.Activo);
        Assert.Equal(0.0, lectura.CorrienteContinua);
        Assert.Equal(0.0, lectura.Voltaje);
        Assert.Equal(0.0, lectura.PotenciaGeneradaWatts);
        Assert.InRange(lectura.TemperaturaCelsius, 18.9, 21.1);
    }

    // ---- Fallas forzadas ---------------------------------------------------------------------

    [Theory]
    [InlineData(15)] // mediodía
    [InlineData(3)]  // medianoche
    public void La_falla_de_sobretemperatura_supera_los_75_grados_a_cualquier_hora(int horaUtc)
    {
        var panel = new PanelSimulado("P01", 300)
        {
            Falla = new FallaActiva(TipoFalla.Sobretemperatura, null)
        };
        var momento = new DateTime(2026, 10, 10, horaUtc, 0, 0, DateTimeKind.Utc);

        var lectura = Crear().Generar(panel, momento);

        Assert.True(lectura.TemperaturaCelsius > 75);
    }

    [Fact]
    public void La_falla_de_caida_de_potencia_reduce_la_potencia_a_la_mitad()
    {
        var panel = new PanelSimulado("P01", 300)
        {
            Falla = new FallaActiva(TipoFalla.CaidaPotencia, null)
        };

        var potencia = Crear().Generar(panel, Mediodia).PotenciaGeneradaWatts;

        Assert.InRange(potencia, 142.5 - 0.01, 157.5 + 0.01); // 300 × 0,5 ± 5 %
    }

    // ---- Fallas aleatorias -------------------------------------------------------------------

    [Fact]
    public void Con_probabilidad_uno_un_panel_sano_entra_en_falla_con_vencimiento()
    {
        var panel = new PanelSimulado("P01", 300);

        Crear(probabilidadFalla: 1.0, duracionFallaSegundos: 120).ActualizarFalla(panel, Mediodia);

        Assert.NotNull(panel.Falla);
        Assert.False(panel.Falla!.EsForzada);
        Assert.Equal(Mediodia.AddSeconds(120), panel.Falla.ExpiraUtc!.Value);
    }

    [Fact]
    public void Con_probabilidad_cero_nunca_aparece_una_falla()
    {
        var generador = Crear(probabilidadFalla: 0);
        var panel = new PanelSimulado("P01", 300);

        for (var i = 0; i < 500; i++)
        {
            generador.ActualizarFalla(panel, Mediodia);
        }

        Assert.Null(panel.Falla);
    }

    [Fact]
    public void Una_falla_aleatoria_vencida_se_recupera_sola()
    {
        var panel = new PanelSimulado("P01", 300)
        {
            Falla = new FallaActiva(TipoFalla.CaidaPotencia, Mediodia.AddSeconds(-1))
        };

        Crear(probabilidadFalla: 0).ActualizarFalla(panel, Mediodia);

        Assert.Null(panel.Falla);
    }

    [Fact]
    public void Una_falla_aleatoria_vigente_se_mantiene()
    {
        var panel = new PanelSimulado("P01", 300)
        {
            Falla = new FallaActiva(TipoFalla.CaidaPotencia, Mediodia.AddSeconds(60))
        };

        Crear(probabilidadFalla: 0).ActualizarFalla(panel, Mediodia);

        Assert.NotNull(panel.Falla);
    }

    [Fact]
    public void Una_falla_forzada_no_se_recupera_sola()
    {
        var panel = new PanelSimulado("P01", 300)
        {
            Falla = new FallaActiva(TipoFalla.Sobretemperatura, null)
        };

        Crear(probabilidadFalla: 0).ActualizarFalla(panel, Mediodia.AddDays(30));

        Assert.NotNull(panel.Falla);
    }

    [Fact]
    public void Un_panel_desactivado_no_entra_en_falla_aleatoria()
    {
        var panel = new PanelSimulado("P01", 300) { Activo = false };

        Crear(probabilidadFalla: 1.0).ActualizarFalla(panel, Mediodia);

        Assert.Null(panel.Falla);
    }

    // ---- Zona horaria ------------------------------------------------------------------------

    [Fact]
    public void Una_zona_horaria_desconocida_cae_a_UTC_menos_3()
    {
        var zona = ZonaHoraria.Resolver("Zona/Que/No/Existe");

        Assert.Equal(TimeSpan.FromHours(-3), zona.GetUtcOffset(new DateTime(2026, 10, 10, 15, 0, 0, DateTimeKind.Utc)));
    }
}
