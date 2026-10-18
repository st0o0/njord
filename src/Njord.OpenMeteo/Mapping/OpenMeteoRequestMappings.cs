using System.Globalization;
using Njord.Core.Configuration;
using Njord.Model;

namespace Njord.OpenMeteo.Mapping;

internal static class OpenMeteoRequestMappings
{
    public static string ToRequestUri(
        this LocationOptions location,
        WeatherModel model,
        int forecastDays,
        string hourlyVariables,
        string dailyVariables)
    {
        var effectiveDays = ModelCoverageRegistry.EffectiveForecastDays(model.Id, forecastDays);
        var uri = string.Create(
            CultureInfo.InvariantCulture,
            $"v1/forecast?latitude={location.Latitude}&longitude={location.Longitude}" +
            $"&models={Uri.EscapeDataString(model.Id)}&hourly={hourlyVariables}" +
            $"&wind_speed_unit=ms&timeformat=unixtime&timezone=UTC&forecast_days={effectiveDays}");

        if (dailyVariables.Length > 0)
        {
            uri += $"&daily={dailyVariables}";
        }

        return uri;
    }
}
