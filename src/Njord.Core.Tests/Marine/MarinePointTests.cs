using Njord.Core.Marine;

namespace Njord.Core.Tests.Marine;

public sealed class MarinePointTests
{
    private static readonly DateTimeOffset At = new(2026, 7, 1, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Should_report_true_when_every_requested_field_has_a_value()
    {
        var point = new MarinePoint(At, WaveHeight: 1, WavePeriod: 2);

        Assert.True(point.HasFields(MarineParams.WaveHeight | MarineParams.WavePeriod));
    }

    [Fact]
    public void Should_report_false_when_one_requested_field_is_null()
    {
        var point = new MarinePoint(At, WaveHeight: 1);

        Assert.False(point.HasFields(MarineParams.WaveHeight | MarineParams.WavePeriod));
    }

    [Fact]
    public void Should_report_true_when_nothing_is_requested()
    {
        Assert.True(new MarinePoint(At).HasFields(MarineParams.None));
    }

    [Fact]
    public void Should_ignore_fields_that_are_not_requested()
    {
        var point = new MarinePoint(At, WaveHeight: 1);

        Assert.True(point.HasFields(MarineParams.WaveHeight));
    }

    [Fact]
    public void Should_report_each_new_flag_true_only_when_its_own_member_has_a_value()
    {
        var point = new MarinePoint(At, WindWaveHeight: 1, WindWavePeriod: 2, WindWaveDirection: 3, WindWavePeakPeriod: 4,
            SwellWaveHeight: 5, SwellWavePeriod: 6, SwellWaveDirection: 7, SwellWavePeakPeriod: 8, SeaLevelHeight: 9);

        Assert.Multiple(
            () => Assert.True(point.HasFields(MarineParams.WindWaveHeight)),
            () => Assert.True(point.HasFields(MarineParams.WindWavePeriod)),
            () => Assert.True(point.HasFields(MarineParams.WindWaveDirection)),
            () => Assert.True(point.HasFields(MarineParams.WindWavePeakPeriod)),
            () => Assert.True(point.HasFields(MarineParams.SwellWaveHeight)),
            () => Assert.True(point.HasFields(MarineParams.SwellWavePeriod)),
            () => Assert.True(point.HasFields(MarineParams.SwellWaveDirection)),
            () => Assert.True(point.HasFields(MarineParams.SwellWavePeakPeriod)),
            () => Assert.True(point.HasFields(MarineParams.SeaLevelHeight)),
            () => Assert.False(point.HasFields(MarineParams.WaveHeight)));
    }

    [Fact]
    public void Should_report_swell_false_when_only_wind_wave_values_exist()
    {
        var point = new MarinePoint(At, WindWaveHeight: 1);

        Assert.Multiple(
            () => Assert.True(point.HasFields(MarineParams.WindWaveHeight)),
            () => Assert.False(point.HasFields(MarineParams.SwellWaveHeight)));
    }

    [Fact]
    public void Should_report_false_for_every_single_flag_on_an_empty_point()
    {
        var point = new MarinePoint(At);

        Assert.All(
            Enum.GetValues<MarineParams>().Where(p => p != MarineParams.None),
            flag => Assert.False(point.HasFields(flag)));
    }
}
