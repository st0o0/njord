using Njord.Forecast.Marine;

namespace Njord.Forecast.Tests.Marine;

public sealed class MarinePointSpec
{
    private static readonly DateTimeOffset At = new(2026, 7, 1, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Should_report_true_when_every_requested_field_has_a_value()
    {
        var point = new MarinePoint(At, WaveHeight: 1, WavePeriod: 2);

        Assert.True(point.HasFields(MarineParam.WaveHeight | MarineParam.WavePeriod));
    }

    [Fact]
    public void Should_report_false_when_one_requested_field_is_null()
    {
        var point = new MarinePoint(At, WaveHeight: 1);

        Assert.False(point.HasFields(MarineParam.WaveHeight | MarineParam.WavePeriod));
    }

    [Fact]
    public void Should_report_true_when_nothing_is_requested()
    {
        Assert.True(new MarinePoint(At).HasFields(MarineParam.None));
    }

    [Fact]
    public void Should_ignore_fields_that_are_not_requested()
    {
        var point = new MarinePoint(At, WaveHeight: 1);

        Assert.True(point.HasFields(MarineParam.WaveHeight));
    }

    [Fact]
    public void Should_report_false_for_every_single_flag_on_an_empty_point()
    {
        var point = new MarinePoint(At);

        Assert.All(
            Enum.GetValues<MarineParam>().Where(p => p != MarineParam.None),
            flag => Assert.False(point.HasFields(flag)));
    }
}
