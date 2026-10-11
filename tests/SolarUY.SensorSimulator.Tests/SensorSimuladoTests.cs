using SolarUY.SensorSimulator.Configuration;
using SolarUY.SensorSimulator.Simulacion;

namespace SolarUY.SensorSimulator.Tests;

public class SensorSimuladoTests
{
    private static readonly DateTime Mediodia = new(2026, 10, 10, 15, 0, 0, DateTimeKind.Utc);

    private static SensorSimulado CrearSensor(int paneles = 3) =>
        SensorSimulado.Crear("ARM-001", paneles, 300, 0.97);

    private static GeneradorLectura CrearGenerador() =>
        new(new SimuladorOptions { ProbabilidadFalla = 0 }, new Random(1));

    [Fact]
    public void Crea_los_paneles_P01_P02_P03()
    {
        var codigos = CrearSensor(3).Inventario().Paneles.Select(p => p.CodigoPanel);

        Assert.Equal(new[] { "P01", "P02", "P03" }, codigos);
    }

    [Fact]
    public void Un_sensor_sin_paneles_no_se_puede_crear()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CrearSensor(0));
    }

    [Fact]
    public void Desactivar_un_panel_activo_funciona_y_repetirlo_es_conflicto()
    {
        var sensor = CrearSensor();

        Assert.Equal(ResultadoOrden.Ok, sensor.CambiarEstadoPanel("P01", false));
        Assert.Equal(ResultadoOrden.YaAplicado, sensor.CambiarEstadoPanel("P01", false));
    }

    [Fact]
    public void Activar_un_panel_ya_activo_es_conflicto()
    {
        Assert.Equal(ResultadoOrden.YaAplicado, CrearSensor().CambiarEstadoPanel("P01", true));
    }

    [Fact]
    public void Se_puede_reactivar_un_panel_desactivado()
    {
        var sensor = CrearSensor();
        sensor.CambiarEstadoPanel("P02", false);

        Assert.Equal(ResultadoOrden.Ok, sensor.CambiarEstadoPanel("P02", true));
    }

    [Fact]
    public void Un_panel_inexistente_se_informa()
    {
        Assert.Equal(ResultadoOrden.PanelInexistente, CrearSensor().CambiarEstadoPanel("P99", false));
    }

    [Fact]
    public void El_codigo_de_panel_no_distingue_mayusculas()
    {
        Assert.Equal(ResultadoOrden.Ok, CrearSensor().CambiarEstadoPanel("p01", false));
    }

    [Fact]
    public void El_volcado_a_la_red_arranca_activo_y_se_puede_cambiar()
    {
        var sensor = CrearSensor();

        Assert.True(sensor.VolcadoRedActivo);
        Assert.Equal(ResultadoOrden.YaAplicado, sensor.CambiarVolcado(true));
        Assert.Equal(ResultadoOrden.Ok, sensor.CambiarVolcado(false));
        Assert.False(sensor.VolcadoRedActivo);
        Assert.Equal(ResultadoOrden.YaAplicado, sensor.CambiarVolcado(false));
        Assert.Equal(ResultadoOrden.Ok, sensor.CambiarVolcado(true));
    }

    [Fact]
    public void Forzar_y_restablecer_una_falla_se_refleja_en_el_inventario()
    {
        var sensor = CrearSensor();

        Assert.Equal(ResultadoOrden.Ok, sensor.ForzarFalla("P01", TipoFalla.Sobretemperatura));
        var conFalla = sensor.Inventario().Paneles.First(p => p.CodigoPanel == "P01");
        Assert.Equal("sobretemperatura", conFalla.Falla);
        Assert.True(conFalla.FallaForzada);

        Assert.Equal(ResultadoOrden.Ok, sensor.RestablecerFalla("P01"));
        Assert.Null(sensor.Inventario().Paneles.First(p => p.CodigoPanel == "P01").Falla);
    }

    [Fact]
    public void Forzar_o_restablecer_falla_en_un_panel_inexistente_se_informa()
    {
        var sensor = CrearSensor();

        Assert.Equal(ResultadoOrden.PanelInexistente, sensor.ForzarFalla("P99", TipoFalla.CaidaPotencia));
        Assert.Equal(ResultadoOrden.PanelInexistente, sensor.RestablecerFalla("P99"));
    }

    [Fact]
    public void Restablecer_un_panel_sin_falla_es_idempotente()
    {
        Assert.Equal(ResultadoOrden.Ok, CrearSensor().RestablecerFalla("P01"));
    }

    [Fact]
    public void LeerEstado_devuelve_todos_los_paneles_y_refleja_un_panel_desactivado()
    {
        var sensor = CrearSensor(3);
        sensor.CambiarEstadoPanel("P02", false);

        var estado = sensor.LeerEstado(CrearGenerador(), Mediodia);

        Assert.Equal("ARM-001", estado.IdentificadorHardwareARM);
        Assert.Equal(3, estado.Paneles.Count);
        Assert.False(estado.Paneles.First(p => p.CodigoPanel == "P02").Activo);
        Assert.True(estado.Paneles.First(p => p.CodigoPanel == "P01").Activo);
    }
}
