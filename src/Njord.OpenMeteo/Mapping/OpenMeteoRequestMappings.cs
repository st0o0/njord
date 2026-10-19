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

    public const string AirQualityHourlyVariables =
        "pm2_5,pm10,ozone,nitrogen_dioxide,european_aqi,alder_pollen,birch_pollen,grass_pollen,mugwort_pollen,ragweed_pollen";

    public static string ToAirQualityRequestUri(double latitude, double longitude, int forecastDays)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"https://air-quality-api.open-meteo.com/v1/air-quality?latitude={latitude}&longitude={longitude}" +
            $"&hourly={AirQualityHourlyVariables}" +
            $"&forecast_days={forecastDays}&timeformat=unixtime&timezone=UTC");
    }

    public const string FloodDailyVariables =
        "river_discharge,river_discharge_mean,river_discharge_median,river_discharge_max,river_discharge_min,river_discharge_p25,river_discharge_p75";

    public const int FloodForecastDays = 92;

    public static string ToFloodRequestUri(double latitude, double longitude)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"https://flood-api.open-meteo.com/v1/flood?latitude={latitude}&longitude={longitude}" +
            $"&daily={FloodDailyVariables}" +
            $"&forecast_days={FloodForecastDays}&timeformat=unixtime&timezone=UTC");
    }
}
