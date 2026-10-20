using Njord.Forecast.AirQuality;
using Njord.Forecast.Flood;
using Njord.Forecast.Marine;
using Njord.Forecast.Weather;

namespace Njord.OpenMeteo.Mapping;

public static class MarineParamsMappings
{
    public static IReadOnlyList<string> ToApiNames(this MarineParams flags) =>
        [.. Enum.GetValues<MarineParams>().Where(p => p != MarineParams.None && flags.HasFlag(p)).Select(ToApiName)];

    private static string ToApiName(MarineParams param) => param switch
    {
        MarineParams.WaveHeight => "wave_height",
        MarineParams.WavePeriod => "wave_period",
        MarineParams.WaveDirection => "wave_direction",
        MarineParams.WaterTemperature => "sea_surface_temperature",
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };
}
