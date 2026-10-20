using Njord.Forecast.Weather;

namespace Njord.Forecast.Tests.Weather;

public sealed class WeatherPointSpec
{
    private static readonly DateTimeOffset At = new(2026, 7, 1, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Should_report_true_when_every_requested_field_has_a_value()
    {
        var point = new WeatherPoint(At, Temperature: 1, WindSpeed: 2);

        Assert.True(point.HasFields(WeatherParams.Temperature | WeatherParams.WindSpeed));
    }

    [Fact]
    public void Should_report_false_when_one_requested_field_is_null()
    {
        var point = new WeatherPoint(At, Temperature: 1);

        Assert.False(point.HasFields(WeatherParams.Temperature | WeatherParams.WindSpeed));
    }

    [Fact]
    public void Should_report_true_when_nothing_is_requested()
    {
        Assert.True(new WeatherPoint(At).HasFields(WeatherParams.None));
    }

    [Fact]
    public void Should_ignore_fields_that_are_not_requested()
    {
        var point = new WeatherPoint(At, Temperature: 1);

        Assert.True(point.HasFields(WeatherParams.Temperature));
    }

    [Fact]
    public void Should_report_false_for_every_single_flag_on_an_empty_point()
    {
        var point = new WeatherPoint(At);

        Assert.All(
            Enum.GetValues<WeatherParams>().Where(p => p != WeatherParams.None),
            flag => Assert.False(point.HasFields(flag)));
    }
}
