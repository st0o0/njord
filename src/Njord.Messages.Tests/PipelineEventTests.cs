using System.Collections.Immutable;
using Njord.Core;
using Njord.Core.Flood;
using Njord.Core.Forecast;
using Njord.Messages.Stream;

namespace Njord.Messages.Tests;

public sealed class PipelineEventTests
{
    private static readonly Location _lucerne = new("Lucerne", 47.05, 8.31);
    private static readonly DateTimeOffset _run = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Should_carry_location_and_forecast_in_a_weather_event()
    {
        var points = ImmutableArray.Create(new ForecastPoint(_run, 20, 19, 3, 0, 0, 0, 1013, 0, 50, 10, 1));
        var forecast = new DomainForecast<ForecastPoint>(DomainKind.Forecast, _run, TimeResolution.Hourly, points);

        PipelineEvent evt = new ForecastDataReceived(_lucerne, forecast);

        var received = Assert.IsType<ForecastDataReceived>(evt);
        Assert.Multiple(
            () => Assert.Equal(_lucerne, received.Location),
            () => Assert.Same(forecast, received.Forecast));
    }

    [Fact]
    public void Should_compare_events_by_value_with_case_insensitive_location()
    {
        var forecast = new DomainForecast<FloodPoint>(DomainKind.Flood, _run, TimeResolution.Daily, []);

        Assert.Equal(
            new FloodDataReceived(_lucerne, forecast),
            new FloodDataReceived(new Location("LUCERNE", 47.05, 8.31), forecast));
    }
}
