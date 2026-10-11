using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using SolarUY.SensorSimulator.Configuration;

namespace SolarUY.SensorSimulator.Simulacion;

/// <summary>
/// Registro en memoria de los sensores simulados (singleton).
/// Los identificadores se comparan sin distinguir mayúsculas: ARM-001 == arm-001.
/// Al reiniciar el simulador se pierde todo y se vuelve a la configuración inicial.
/// </summary>
public sealed class SensorStore
{
    private readonly ConcurrentDictionary<string, SensorSimulado> _sensores =
        new(StringComparer.OrdinalIgnoreCase);

    public int Cantidad => _sensores.Count;

    /// <summary>
    /// Crea los sensores iniciales. Si la configuración trae la lista "Nodos" se usa esa topología;
    /// si no, se crean NodosIniciales sensores con PanelesPorNodo paneles cada uno.
    /// </summary>
    public void CargarIniciales(SimuladorOptions opciones)
    {
        if (opciones.Nodos.Count > 0)
        {
            foreach (var nodo in opciones.Nodos)
            {
                Agregar(nodo.Id, nodo.Paneles, nodo.PotenciaNominalW ?? opciones.PotenciaNominalPanelW, opciones.EficienciaConversion);
            }

            return;
        }

        for (var i = 1; i <= opciones.NodosIniciales; i++)
        {
            Agregar($"{opciones.PrefijoHardware}{i:D3}", opciones.PanelesPorNodo, opciones.PotenciaNominalPanelW, opciones.EficienciaConversion);
        }
    }

    /// <returns>false si ya existía un sensor con ese identificador.</returns>
    public bool Agregar(string identificadorHardwareARM, int cantidadPaneles, double potenciaNominalW, double eficienciaConversion)
    {
        var sensor = SensorSimulado.Crear(identificadorHardwareARM, cantidadPaneles, potenciaNominalW, eficienciaConversion);
        return _sensores.TryAdd(identificadorHardwareARM, sensor);
    }

    public bool Quitar(string identificadorHardwareARM) =>
        _sensores.TryRemove(identificadorHardwareARM, out _);

    public bool TryObtener(string identificadorHardwareARM, [NotNullWhen(true)] out SensorSimulado? sensor) =>
        _sensores.TryGetValue(identificadorHardwareARM, out sensor);

    public IReadOnlyList<SensorSimulado> Listar() =>
        _sensores.Values
            .OrderBy(s => s.IdentificadorHardwareARM, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
