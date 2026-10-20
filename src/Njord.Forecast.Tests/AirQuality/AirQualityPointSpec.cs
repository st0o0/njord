using Njord.Forecast.AirQuality;

namespace Njord.Forecast.Tests.AirQuality;

public sealed class AirQualityPointSpec
{
    private static readonly DateTimeOffset At = new(2026, 7, 1, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Should_report_true_when_every_requested_field_has_a_value()
    {
        var point = new AirQualityPoint(At, Pm25: 1, Pm10: 2);

        Assert.True(point.HasFields(AirQualityParams.Pm25 | AirQualityParams.Pm10));
    }

    [Fact]
    public void Should_report_false_when_one_requested_field_is_null()
    {
        var point = new AirQualityPoint(At, Pm25: 1);

        Assert.False(point.HasFields(AirQualityParams.Pm25 | AirQualityParams.Pm10));
    }

    [Fact]
    public void Should_report_true_when_nothing_is_requested()
    {
        Assert.True(new AirQualityPoint(At).HasFields(AirQualityParams.None));
    }

    [Fact]
    public void Should_ignore_fields_that_are_not_requested()
    {
        var point = new AirQualityPoint(At, Pm25: 1);

        Assert.True(point.HasFields(AirQualityParams.Pm25));
    }

    [Fact]
    public void Should_report_false_for_every_single_flag_on_an_empty_point()
    {
        var point = new AirQualityPoint(At);

        Assert.All(
            Enum.GetValues<AirQualityParams>().Where(p => p != AirQualityParams.None),
            flag => Assert.False(point.HasFields(flag)));
    }

    [Fact]
    public void Should_report_true_for_every_single_flag_when_all_fields_have_values()
    {
        var point = new AirQualityPoint(
            At, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);

        Assert.All(
            Enum.GetValues<AirQualityParams>().Where(p => p != AirQualityParams.None),
            flag => Assert.True(point.HasFields(flag), flag.ToString()));
    }
}
