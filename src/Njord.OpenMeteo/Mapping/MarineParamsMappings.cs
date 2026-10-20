using Njord.Forecast.Marine;

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
        MarineParams.WindWaveHeight => "wind_wave_height",
        MarineParams.WindWavePeriod => "wind_wave_period",
        MarineParams.WindWaveDirection => "wind_wave_direction",
        MarineParams.WindWavePeakPeriod => "wind_wave_peak_period",
        MarineParams.SwellWaveHeight => "swell_wave_height",
        MarineParams.SwellWavePeriod => "swell_wave_period",
        MarineParams.SwellWaveDirection => "swell_wave_direction",
        MarineParams.SwellWavePeakPeriod => "swell_wave_peak_period",
        MarineParams.SeaLevelHeight => "sea_level_height_msl",
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };
}
