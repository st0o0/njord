using Njord.Forecast;
using Njord.Forecast.Marine;
using static Njord.OpenMeteo.Mapping.ForecastBuilding;
using static Njord.OpenMeteo.Mapping.SeriesAccess;

namespace Njord.OpenMeteo.Mapping;

internal static class MarineResponseMappings
{
    public static DomainForecast<MarinePoint>? ToDomainForecast(
        this MarineResponse response, DateTimeOffset modelRun)
    {
        if (response.Hourly is not { } h || h.Time.Length == 0)
        {
            return null;
        }

        return BuildForecast(
            DomainKind.Marine, TimeResolution.Hourly, modelRun, h.Time.Length,
            i => AnyAt(i, h.WaveHeight, h.WavePeriod, h.WaveDirection, h.SeaSurfaceTemperature,
                h.WindWaveHeight, h.WindWavePeriod, h.WindWaveDirection, h.WindWavePeakPeriod,
                h.SwellWaveHeight, h.SwellWavePeriod, h.SwellWaveDirection, h.SwellWavePeakPeriod, h.SeaLevelHeight),
            i => new MarinePoint(
                DateTimeOffset.FromUnixTimeSeconds(h.Time[i]),
                WaveHeight: At(h.WaveHeight, i),
                WavePeriod: At(h.WavePeriod, i),
                WaveDirection: At(h.WaveDirection, i),
                WaterTemperature: At(h.SeaSurfaceTemperature, i),
                WindWaveHeight: At(h.WindWaveHeight, i),
                WindWavePeriod: At(h.WindWavePeriod, i),
                WindWaveDirection: At(h.WindWaveDirection, i),
                WindWavePeakPeriod: At(h.WindWavePeakPeriod, i),
                SwellWaveHeight: At(h.SwellWaveHeight, i),
                SwellWavePeriod: At(h.SwellWavePeriod, i),
                SwellWaveDirection: At(h.SwellWaveDirection, i),
                SwellWavePeakPeriod: At(h.SwellWavePeakPeriod, i),
                SeaLevelHeight: At(h.SeaLevelHeight, i)));
    }
}
