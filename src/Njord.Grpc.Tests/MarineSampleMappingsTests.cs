using Njord.Core.Marine;
using Njord.Grpc.Mapping;

namespace Njord.Grpc.Tests;

public sealed class MarineSampleMappingsTests
{
    private static readonly DateTimeOffset At = new(2026, 7, 1, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Swell_wind_wave_and_sea_level_values_map_to_their_own_fields()
    {
        var sample = new MarinePoint(At, WaveHeight: 1.5, WindWaveHeight: 0.5, WindWavePeriod: 4, WindWaveDirection: 90,
            WindWavePeakPeriod: 5, SwellWaveHeight: 1.4, SwellWavePeriod: 12, SwellWaveDirection: 270,
            SwellWavePeakPeriod: 14, SeaLevelHeight: 0.3).ToProto();

        Assert.Multiple(
            () => Assert.Equal(1.5, sample.WaveHeight),
            () => Assert.Equal(0.5, sample.WindWaveHeight),
            () => Assert.Equal(4, sample.WindWavePeriod),
            () => Assert.Equal(90, sample.WindWaveDirection),
            () => Assert.Equal(5, sample.WindWavePeakPeriod),
            () => Assert.Equal(1.4, sample.SwellWaveHeight),
            () => Assert.Equal(12, sample.SwellWavePeriod),
            () => Assert.Equal(270, sample.SwellWaveDirection),
            () => Assert.Equal(14, sample.SwellWavePeakPeriod),
            () => Assert.Equal(0.3, sample.SeaLevelHeightMsl));
    }

    [Fact]
    public void Missing_swell_values_stay_unset()
    {
        var sample = new MarinePoint(At, WaveHeight: 1.5).ToProto();

        Assert.Multiple(
            () => Assert.False(sample.HasSwellWaveHeight),
            () => Assert.False(sample.HasWindWaveHeight),
            () => Assert.False(sample.HasSeaLevelHeightMsl));
    }
}
