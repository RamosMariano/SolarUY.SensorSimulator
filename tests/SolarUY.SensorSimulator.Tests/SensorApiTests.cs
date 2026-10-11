using System.Net;

namespace SolarUY.SensorSimulator.Tests;

/// <summary>Prueba el contrato que consume el sistema de gestión (sección 3 de la spec).</summary>
public class SensorApiTests
{
    [Fact]
    public async Task Estado_devuelve_los_campos_del_contrato_en_camelCase()
    {
        using var fabrica = new SimuladorFactory();
        using var cliente = fabrica.CrearCliente();

        var respuesta = await cliente.GetAsync("/sensores/ARM-001/estado");
        var estado = await respuesta.LeerJsonAsync();

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("ARM-001", estado.GetProperty("identificadorHardwareARM").GetString());
        Assert.True(estado.GetProperty("volcadoRedActivo").GetBoolean());
        Assert.Equal(0.97, estado.GetProperty("eficienciaConversion").GetDouble(), 2);
        Assert.True(estado.TryGetProperty("fechaHoraUtc", out _));

        var paneles = estado.GetProperty("paneles");
        Assert.Equal(5, paneles.GetArrayLength());

        var primero = paneles[0];
        Assert.Equal("P01", primero.GetProperty("codigoPanel").GetString());
        Assert.True(primero.GetProperty("activo").GetBoolean());
        Assert.True(primero.TryGetProperty("corrienteContinua", out _));
        Assert.True(primero.TryGetProperty("voltaje", out _));
        Assert.True(primero.TryGetProperty("potenciaGeneradaWatts", out _));
        Assert.True(primero.TryGetProperty("temperaturaCelsius", out _));
    }

    [Fact]
    public async Task Estado_no_incluye_EstadoDetectado_porque_lo_calcula_el_sistema()
    {
        using var fabrica = new SimuladorFactory();
        using var cliente = fabrica.CrearCliente();

        var estado = await (await cliente.GetAsync("/sensores/ARM-001/estado")).LeerJsonAsync();

        Assert.False(estado.Panel("P01").TryGetProperty("estadoDetectado", out _));
    }

    [Fact]
    public async Task Al_mediodia_los_paneles_generan_potencia_cercana_a_la_nominal()
    {
        using var fabrica = new SimuladorFactory();
        using var cliente = fabrica.CrearCliente();

        var estado = await (await cliente.GetAsync("/sensores/ARM-001/estado")).LeerJsonAsync();

        var potencia = estado.Panel("P01").GetProperty("potenciaGeneradaWatts").GetDouble();
        Assert.InRange(potencia, 284.9, 315.1); // 300 W ± 5 %
    }

    [Fact]
    public async Task Estado_de_un_sensor_inexistente_responde_404()
    {
        using var fabrica = new SimuladorFactory();
        using var cliente = fabrica.CrearCliente();

        var respuesta = await cliente.GetAsync("/sensores/ARM-999/estado");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task El_identificador_de_sensor_no_distingue_mayusculas()
    {
        using var fabrica = new SimuladorFactory();
        using var cliente = fabrica.CrearCliente();

        var respuesta = await cliente.GetAsync("/sensores/arm-001/estado");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    // ---- Órdenes sobre paneles ---------------------------------------------------------------

    [Fact]
    public async Task Desactivar_un_panel_responde_200_con_el_estado_resultante()
    {
        using var fabrica = new SimuladorFactory();
        using var cliente = fabrica.CrearCliente();

        var respuesta = await cliente.PostAsync("/sensores/ARM-001/paneles/P01/desactivar", null);
        var estado = await respuesta.LeerJsonAsync();

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var panel = estado.Panel("P01");
        Assert.False(panel.GetProperty("activo").GetBoolean());
        Assert.Equal(0.0, panel.GetProperty("potenciaGeneradaWatts").GetDouble());
        Assert.True(estado.Panel("P02").GetProperty("activo").GetBoolean());
    }

    [Fact]
    public async Task Desactivar_dos_veces_el_mismo_panel_responde_409()
    {
        using var fabrica = new SimuladorFactory();
        using var cliente = fabrica.CrearCliente();

        await cliente.PostAsync("/sensores/ARM-001/paneles/P01/desactivar", null);
        var segunda = await cliente.PostAsync("/sensores/ARM-001/paneles/P01/desactivar", null);

        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
    }

    [Fact]
    public async Task Activar_un_panel_ya_activo_responde_409()
    {
        using var fabrica = new SimuladorFactory();
        using var cliente = fabrica.CrearCliente();

        var respuesta = await cliente.PostAsync("/sensores/ARM-001/paneles/P01/activar", null);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
    }

    [Fact]
    public async Task Reactivar_un_panel_desactivado_vuelve_a_generar_potencia()
    {
        using var fabrica = new SimuladorFactory();
        using var cliente = fabrica.CrearCliente();
        await cliente.PostAsync("/sensores/ARM-001/paneles/P01/desactivar", null);

        var respuesta = await cliente.PostAsync("/sensores/ARM-001/paneles/P01/activar", null);
        var estado = await respuesta.LeerJsonAsync();

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var panel = estado.Panel("P01");
        Assert.True(panel.GetProperty("activo").GetBoolean());
        Assert.True(panel.GetProperty("potenciaGeneradaWatts").GetDouble() > 0);
    }

    [Theory]
    [InlineData("/sensores/ARM-001/paneles/P99/activar")]
    [InlineData("/sensores/ARM-001/paneles/P99/desactivar")]
    [InlineData("/sensores/ARM-999/paneles/P01/desactivar")]
    public async Task Ordenes_sobre_un_panel_o_sensor_inexistente_responden_404(string ruta)
    {
        using var fabrica = new SimuladorFactory();
        using var cliente = fabrica.CrearCliente();

        var respuesta = await cliente.PostAsync(ruta, null);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    // ---- Órdenes sobre el volcado a la red ---------------------------------------------------

    [Fact]
    public async Task Desactivar_y_reactivar_el_volcado_a_la_red()
    {
        using var fabrica = new SimuladorFactory();
        using var cliente = fabrica.CrearCliente();

        var desactivar = await cliente.PostAsync("/sensores/ARM-001/volcado/desactivar", null);
        Assert.Equal(HttpStatusCode.OK, desactivar.StatusCode);
        Assert.False((await desactivar.LeerJsonAsync()).GetProperty("volcadoRedActivo").GetBoolean());

        var activar = await cliente.PostAsync("/sensores/ARM-001/volcado/activar", null);
        Assert.Equal(HttpStatusCode.OK, activar.StatusCode);
        Assert.True((await activar.LeerJsonAsync()).GetProperty("volcadoRedActivo").GetBoolean());
    }

    [Fact]
    public async Task Repetir_una_orden_de_volcado_responde_409()
    {
        using var fabrica = new SimuladorFactory();
        using var cliente = fabrica.CrearCliente();

        var respuesta = await cliente.PostAsync("/sensores/ARM-001/volcado/activar", null); // ya estaba activo

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
    }

    [Fact]
    public async Task Orden_de_volcado_sobre_un_sensor_inexistente_responde_404()
    {
        using var fabrica = new SimuladorFactory();
        using var cliente = fabrica.CrearCliente();

        var respuesta = await cliente.PostAsync("/sensores/ARM-999/volcado/desactivar", null);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}
