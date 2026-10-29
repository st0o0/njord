using Njord.Core;
using Njord.Core.Forecast;
using Njord.Core.Marine;
using static Njord.OpenMeteo.Mapping.SeriesAccess;

namespace Njord.OpenMeteo.Marine;

internal static class MarineResponseMappingExtensions
{
    public static DomainForecast<MarinePoint>? ToDomainForecast(
        this MarineResponse response, DateTimeOffset modelRun)
    {
        if (response.Hourly is not { } h || h.Time.Length == 0)
        {
            return null;
        }

        var points = new MarinePoint[h.Time.Length];
        for (var i = 0; i < h.Time.Length; i++)
        {
            points[i] = new MarinePoint(
                ValidFrom: DateTimeOffset.FromUnixTimeSeconds(h.Time[i]),
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
                SeaLevelHeight: At(h.SeaLevelHeight, i),
                OceanCurrentVelocity: At(h.OceanCurrentVelocity, i),
                OceanCurrentDirection: At(h.OceanCurrentDirection, i),
                SecondarySwellWaveHeight: At(h.SecondarySwellWaveHeight, i),
                SecondarySwellWaveDirection: At(h.SecondarySwellWaveDirection, i),
                SecondarySwellWavePeriod: At(h.SecondarySwellWavePeriod, i),
                TertiarySwellWaveHeight: At(h.TertiarySwellWaveHeight, i),
                TertiarySwellWaveDirection: At(h.TertiarySwellWaveDirection, i),
                TertiarySwellWavePeriod: At(h.TertiarySwellWavePeriod, i),
                InvertBarometerHeight: At(h.InvertBarometerHeight, i));
        }

        return new DomainForecast<MarinePoint>(
            DomainKind.Marine, modelRun, TimeResolution.Hourly, [.. points]);
    }
}
