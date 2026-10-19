using System.Globalization;
using Njord.Core.Configuration;

namespace Njord.OpenMeteo.Mapping;

internal static class OpenMeteoRequestMappings
{
    public static string ToEnsembleRequestUri(
        this LocationOptions location,
        int forecastDays,
        string hourlyVariables)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"v1/ensemble?latitude={location.Latitude}&longitude={location.Longitude}" +
            $"&hourly={hourlyVariables}" +
            $"&wind_speed_unit=ms&timeformat=unixtime&timezone=UTC&forecast_days={forecastDays}");
    }
}
