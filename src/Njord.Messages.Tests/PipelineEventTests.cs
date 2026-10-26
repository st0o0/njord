using System.Collections.Immutable;
using Njord.Forecast;
using Njord.Forecast.Flood;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;

namespace Njord.Messages.Tests;

public sealed class PipelineEventTests
{
    private static readonly Location Lucerne = new("Lucerne", 47.05, 8.31);
    private static readonly DateTimeOffset Run = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Should_carry_location_and_forecast_in_a_weather_event()
    {
        var points = ImmutableArray.Create(new WeatherPoint(Run, 20, 19, 3, 0, 0, 0, 1013, 0, 50, 10, 1));
        var forecast = new DomainForecast<WeatherPoint>(DomainKind.Weather, Run, TimeResolution.Hourly, points);

        PipelineEvent evt = new PipelineEvent.WeatherDataReceived(Lucerne, forecast);

        var received = Assert.IsType<PipelineEvent.WeatherDataReceived>(evt);
        Assert.Multiple(
            () => Assert.Equal(Lucerne, received.Location),
            () => Assert.Same(forecast, received.Forecast));
    }

    [Fact]
    public void Should_compare_events_by_value_with_case_insensitive_location()
    {
        var forecast = new DomainForecast<FloodPoint>(DomainKind.Flood, Run, TimeResolution.Daily, []);

        Assert.Equal(
            new PipelineEvent.FloodDataReceived(Lucerne, forecast),
            new PipelineEvent.FloodDataReceived(new Location("LUCERNE", 47.05, 8.31), forecast));
    }
}
