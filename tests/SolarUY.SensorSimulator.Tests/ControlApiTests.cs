using System.Net;
using System.Net.Http.Json;

namespace SolarUY.SensorSimulator.Tests;

/// <summary>Prueba la API de control del simulador (sección 4 de la spec): alta de nodos y fallas forzadas.</summary>
public class ControlApiTests
{
    [Fact]
    public async Task Inventario_lista_los_sensores_iniciales_con_sus_paneles()
    {
        using var fabrica = new SimuladorFactory();
        using var cliente = fabrica.CrearCliente();

        var respuesta = await cliente.GetAsync("/simulador/inventario");
        var inventario = await respuesta.LeerJsonAsync();

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(3, inventario.GetArrayLength());
        Assert.Equal("ARM-001", inventario[0].GetProperty("identificadorHardwareARM").GetString());
        Assert.Equal(5, inventario[0].GetProperty("paneles").GetArrayLength());
        Assert.Equal("P01", inventario[0].GetProperty("paneles")[0].GetProperty("codigoPanel").GetString());
    }

    // ---- Alta y baja de sensores en caliente -------------------------------------------------

    [Fact]
    public async Task Agregar_un_sensor_con_seis_paneles_de_400_W_lo_deja_disponible()
    {
        using var fabrica = new SimuladorFactory();
        using var cliente = fabrica.CrearCliente();

        var alta = await cliente.PostAsJsonAsync("/simulador/sensores",
            new { identificadorHardwareARM = "ARM-100", cantidadPaneles = 6, potenciaNominalW = 400 });

        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);

        var estado = await (await cliente.GetAsync("/sensores/ARM-100/estado")).LeerJsonAsync();
        Assert.Equal(6, estado.GetProperty("paneles").GetArrayLength());
        var potencia = estado.Panel("P01").GetProperty("potenciaGeneradaWatts").GetDouble();
        Assert.InRange(potencia, 379.9, 420.1); // 400 W ± 5 %
    }

    [Fact]
    public async Task Sin_potencia_nominal_usa_la_de_la_configuracion()
    {
        using var fabrica = new SimuladorFactory();
        using var cliente = fabrica.CrearCliente();

        await cliente.PostAsJsonAsync("/simulador/sensores",
            new { identificadorHardwareARM = "ARM-101", cantidadPaneles = 2 });

        var inventario = await (await cliente.GetAsync("/simulador/inventario")).LeerJsonAsync();
        var nuevo = inventario.EnumerateArray()
            .First(s => s.GetProperty("identificadorHardwareARM").GetString() == "ARM-101");
        Assert.Equal(300.0, nuevo.GetProperty("paneles")[0].GetProperty("potenciaNominalW").GetDouble());
    }

    [Fact]
    public async Task Agregar_un_sensor_repetido_responde_409()
    {
        using var fabrica = new SimuladorFactory();
        using var cliente = fabrica.CrearCliente();

        var respuesta = await cliente.PostAsJsonAsync("/simulador/sensores",
            new { identificadorHardwareARM = "ARM-001", cantidadPaneles = 2 });

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
    }

    [Theory]
    [InlineData("ARM-200", 0)]
    [InlineData("ARM-200", 201)]
    [InlineData("ARM 200", 3)]
    [InlineData("", 3)]
    public async Task Agregar_un_sensor_con_datos_invalidos_responde_400(string identificador, int cantidadPaneles)
    {
        using var fabrica = new SimuladorFactory();
        using var cliente = fabrica.CrearCliente();

        var respuesta = await cliente.PostAsJsonAsync("/simulador/sensores",
            new { identificadorHardwareARM = identificador, cantidadPaneles });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Agregar_un_sensor_con_potencia_negativa_responde_400()
    {
        using var fabrica = new SimuladorFactory();
        using var cliente = fabrica.CrearCliente();

        var respuesta = await cliente.PostAsJsonAsync("/simulador/sensores",
            new { identificadorHardwareARM = "ARM-300", cantidadPaneles = 2, potenciaNominalW = -5 });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Quitar_un_sensor_lo_hace_desaparecer()
    {
        using var fabrica = new SimuladorFactory();
        using var cliente = fabrica.CrearCliente();

        var baja = await cliente.DeleteAsync("/simulador/sensores/ARM-002");
        var estado = await cliente.GetAsync("/sensores/ARM-002/estado");
        var bajaRepetida = await cliente.DeleteAsync("/simulador/sensores/ARM-002");

        Assert.Equal(HttpStatusCode.NoContent, baja.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, estado.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, bajaRepetida.StatusCode);
    }

    // ---- Fallas forzadas ---------------------------------------------------------------------

    [Fact]
    public async Task Forzar_sobretemperatura_hace_que_el_panel_supere_los_75_grados()
    {
        using var fabrica = new SimuladorFactory();
        using var cliente = fabrica.CrearCliente();

        var forzar = await cliente.PostAsync("/simulador/fallas/ARM-001/P02?tipo=sobretemperatura", null);
        var estado = await (await cliente.GetAsync("/sensores/ARM-001/estado")).LeerJsonAsync();

        Assert.Equal(HttpStatusCode.OK, forzar.StatusCode);
        Assert.True(estado.Panel("P02").GetProperty("temperaturaCelsius").GetDouble() > 75);
        Assert.True(estado.Panel("P01").GetProperty("temperaturaCelsius").GetDouble() < 75);
    }

    [Fact]
    public async Task Forzar_caida_de_potencia_reduce_la_potencia_del_panel_a_la_mitad()
    {
        using var fabrica = new SimuladorFactory();
        using var cliente = fabrica.CrearCliente();

        await cliente.PostAsync("/simulador/fallas/ARM-001/P03?tipo=caidaPotencia", null);
        var estado = await (await cliente.GetAsync("/sensores/ARM-001/estado")).LeerJsonAsync();

        var potencia = estado.Panel("P03").GetProperty("potenciaGeneradaWatts").GetDouble();
        Assert.InRange(potencia, 142.4, 157.6); // 300 W × 0,5 ± 5 %
    }

    [Fact]
    public async Task Restablecer_la_falla_devuelve_el_panel_a_la_normalidad()
    {
        using var fabrica = new SimuladorFactory();
        using var cliente = fabrica.CrearCliente();
        await cliente.PostAsync("/simulador/fallas/ARM-001/P02?tipo=sobretemperatura", null);

        var restablecer = await cliente.DeleteAsync("/simulador/fallas/ARM-001/P02");
        var estado = await (await cliente.GetAsync("/sensores/ARM-001/estado")).LeerJsonAsync();

        Assert.Equal(HttpStatusCode.NoContent, restablecer.StatusCode);
        Assert.True(estado.Panel("P02").GetProperty("temperaturaCelsius").GetDouble() < 75);
    }

    [Theory]
    [InlineData("/simulador/fallas/ARM-001/P02")]                          // falta el parámetro tipo
    [InlineData("/simulador/fallas/ARM-001/P02?tipo=explosion")]           // tipo desconocido
    public async Task Forzar_una_falla_con_tipo_invalido_responde_400(string ruta)
    {
        using var fabrica = new SimuladorFactory();
        using var cliente = fabrica.CrearCliente();

        var respuesta = await cliente.PostAsync(ruta, null);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Theory]
    [InlineData("/simulador/fallas/ARM-001/P99?tipo=sobretemperatura")]
    [InlineData("/simulador/fallas/ARM-999/P01?tipo=sobretemperatura")]
    public async Task Forzar_una_falla_en_un_panel_o_sensor_inexistente_responde_404(string ruta)
    {
        using var fabrica = new SimuladorFactory();
        using var cliente = fabrica.CrearCliente();

        var respuesta = await cliente.PostAsync(ruta, null);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Theory]
    [InlineData("/simulador/fallas/ARM-001/P99")]
    [InlineData("/simulador/fallas/ARM-999/P01")]
    public async Task Restablecer_una_falla_en_un_panel_o_sensor_inexistente_responde_404(string ruta)
    {
        using var fabrica = new SimuladorFactory();
        using var cliente = fabrica.CrearCliente();

        var respuesta = await cliente.DeleteAsync(ruta);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}
