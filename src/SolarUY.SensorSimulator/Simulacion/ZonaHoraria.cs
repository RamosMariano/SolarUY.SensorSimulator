namespace SolarUY.SensorSimulator.Simulacion;

public static class ZonaHoraria
{
    /// <summary>
    /// Resuelve la zona horaria configurada. Si el sistema no la conoce (por ejemplo, un Windows
    /// sin soporte de IDs IANA) prueba con el ID de Windows y, como último recurso, usa UTC-3 fijo.
    /// Uruguay no tiene horario de verano desde 2015, por eso un desfase fijo es correcto.
    /// </summary>
    public static TimeZoneInfo Resolver(string? id)
    {
        foreach (var candidato in new[] { id, "Montevideo Standard Time" })
        {
            if (string.IsNullOrWhiteSpace(candidato))
            {
                continue;
            }

            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(candidato);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone("UY", TimeSpan.FromHours(-3), "Uruguay", "Uruguay");
    }
}
