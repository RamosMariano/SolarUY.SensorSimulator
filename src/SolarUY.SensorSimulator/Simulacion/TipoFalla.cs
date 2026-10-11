namespace SolarUY.SensorSimulator.Simulacion;

public enum TipoFalla
{
    /// <summary>La temperatura del panel supera los 75 °C.</summary>
    Sobretemperatura,

    /// <summary>La potencia generada cae a la mitad.</summary>
    CaidaPotencia
}

public static class TipoFallaExtensions
{
    /// <summary>Nombre que se usa en la API (query string y JSON).</summary>
    public static string ANombreApi(this TipoFalla tipo) => tipo switch
    {
        TipoFalla.Sobretemperatura => "sobretemperatura",
        TipoFalla.CaidaPotencia => "caidaPotencia",
        _ => tipo.ToString()
    };

    public static bool TryParseApi(string? texto, out TipoFalla tipo)
    {
        switch (texto?.Trim().ToLowerInvariant())
        {
            case "sobretemperatura":
                tipo = TipoFalla.Sobretemperatura;
                return true;
            case "caidapotencia":
                tipo = TipoFalla.CaidaPotencia;
                return true;
            default:
                tipo = default;
                return false;
        }
    }
}
