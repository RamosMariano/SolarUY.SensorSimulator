using SolarUY.SensorSimulator.Configuration;
using SolarUY.SensorSimulator.Simulacion;

namespace SolarUY.SensorSimulator.Tests;

public class SensorStoreTests
{
    [Fact]
    public void Con_la_configuracion_por_defecto_crea_ARM_001_a_ARM_003_con_cinco_paneles()
    {
        var store = new SensorStore();

        store.CargarIniciales(new SimuladorOptions { NodosIniciales = 3, PanelesPorNodo = 5 });

        Assert.Equal(new[] { "ARM-001", "ARM-002", "ARM-003" }, store.Listar().Select(s => s.IdentificadorHardwareARM));
        Assert.All(store.Listar(), s => Assert.Equal(5, s.Inventario().Paneles.Count));
    }

    [Fact]
    public void Una_lista_explicita_de_nodos_reemplaza_a_NodosIniciales()
    {
        var store = new SensorStore();
        var opciones = new SimuladorOptions
        {
            NodosIniciales = 10,
            Nodos =
            {
                new NodoConfig { Id = "ARM-001", Paneles = 6 },
                new NodoConfig { Id = "ARM-002", Paneles = 4, PotenciaNominalW = 450 }
            }
        };

        store.CargarIniciales(opciones);

        Assert.Equal(2, store.Cantidad);
        Assert.True(store.TryObtener("ARM-001", out var primero));
        Assert.Equal(6, primero!.Inventario().Paneles.Count);
        Assert.True(store.TryObtener("ARM-002", out var segundo));
        Assert.Equal(4, segundo!.Inventario().Paneles.Count);
        Assert.All(segundo.Inventario().Paneles, p => Assert.Equal(450.0, p.PotenciaNominalW));
    }

    [Fact]
    public void Agregar_un_identificador_repetido_devuelve_false()
    {
        var store = new SensorStore();

        Assert.True(store.Agregar("ARM-100", 4, 300, 0.97));
        Assert.False(store.Agregar("ARM-100", 4, 300, 0.97));
    }

    [Fact]
    public void Los_identificadores_no_distinguen_mayusculas()
    {
        var store = new SensorStore();
        store.Agregar("ARM-100", 4, 300, 0.97);

        Assert.True(store.TryObtener("arm-100", out _));
        Assert.False(store.Agregar("arm-100", 4, 300, 0.97));
    }

    [Fact]
    public void Quitar_elimina_el_sensor_y_repetirlo_devuelve_false()
    {
        var store = new SensorStore();
        store.Agregar("ARM-100", 4, 300, 0.97);

        Assert.True(store.Quitar("ARM-100"));
        Assert.False(store.TryObtener("ARM-100", out _));
        Assert.False(store.Quitar("ARM-100"));
    }
}
