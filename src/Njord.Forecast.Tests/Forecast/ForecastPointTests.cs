using Njord.Forecast.Forecast;

namespace Njord.Forecast.Tests.Forecast;

public sealed class ForecastPointTests
{
    private static readonly DateTimeOffset At = new(2026, 7, 1, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Should_report_true_when_every_requested_field_has_a_value()
    {
        var point = new ForecastPoint(At, Temperature: 1, WindSpeed: 2);

        Assert.True(point.HasFields(ForecastParams.Temperature | ForecastParams.WindSpeed));
    }

    [Fact]
    public void Should_report_false_when_one_requested_field_is_null()
    {
        var point = new ForecastPoint(At, Temperature: 1);

        Assert.False(point.HasFields(ForecastParams.Temperature | ForecastParams.WindSpeed));
    }

    [Fact]
    public void Should_report_true_for_each_extended_field_when_it_has_a_value()
    {
        var point = new ForecastPoint(At, GlobalTiltedIrradiance: 1, SnowDepth: 2, WetBulbTemperature: 3,
            SnowfallHeight: 4);

        Assert.Multiple(
            () => Assert.True(point.HasFields(ForecastParams.GlobalTiltedIrradiance | ForecastParams.SnowDepth | ForecastParams.WetBulbTemperature)),
            () => Assert.True(point.HasFields(ForecastParams.SnowfallHeight)),
            () => Assert.False(new ForecastPoint(At).HasFields(ForecastParams.GlobalTiltedIrradiance)));
    }

    [Fact]
    public void Should_report_true_when_nothing_is_requested()
    {
        Assert.True(new ForecastPoint(At).HasFields(ForecastParams.None));
    }

    [Fact]
    public void Should_ignore_fields_that_are_not_requested()
    {
        var point = new ForecastPoint(At, Temperature: 1);

        Assert.True(point.HasFields(ForecastParams.Temperature));
    }

    [Fact]
    public void Should_report_false_for_every_single_flag_on_an_empty_point()
    {
        var point = new ForecastPoint(At);

        Assert.All(
            Enum.GetValues<ForecastParams>().Where(p => p != ForecastParams.None),
            flag => Assert.False(point.HasFields(flag)));
    }
}
