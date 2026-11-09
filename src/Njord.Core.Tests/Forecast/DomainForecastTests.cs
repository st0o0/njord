using System.Collections.Immutable;
using Njord.Core.Flood;
using Njord.Core.Forecast;
using Njord.Core.Marine;

namespace Njord.Core.Tests.Forecast;

public sealed class DomainForecastTests
{
    private static readonly DateTimeOffset _run = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Should_report_first_and_last_valid_time_from_points()
    {
        var points = ImmutableArray.Create(
            new MarinePoint(_run.AddHours(1), 1.0, 5, 180),
            new MarinePoint(_run.AddHours(2), 1.2, 5, 180),
            new MarinePoint(_run.AddHours(3), 1.4, 6, 190));
        var forecast = new DomainForecast<MarinePoint>(DomainKind.Marine, _run, TimeResolution.Hourly, points);

        Assert.Multiple(
            () => Assert.Equal(_run.AddHours(1), forecast.FirstValid),
            () => Assert.Equal(_run.AddHours(3), forecast.LastValid));
    }

    [Fact]
    public void Should_return_default_valid_times_when_empty()
    {
        var forecast = new DomainForecast<FloodPoint>(DomainKind.Flood, _run, TimeResolution.Daily, []);

        Assert.Multiple(
            () => Assert.Equal(default, forecast.FirstValid),
            () => Assert.Equal(default, forecast.LastValid));
    }

    [Fact]
    public void Should_return_default_valid_times_when_points_array_is_uninitialized()
    {
        var forecast = new DomainForecast<MarinePoint>(DomainKind.Marine, _run, TimeResolution.Hourly, default);

        Assert.Equal(default, forecast.FirstValid);
    }

    [Fact]
    public void Should_expose_metadata_through_the_domain_forecast_interface()
    {
        var points = ImmutableArray.Create(new ForecastPoint(_run, 20, 19, 3, 0, 0, 0, 1013, 0, 50, 10, 1));
        IDomainForecast forecast = new DomainForecast<ForecastPoint>(DomainKind.Forecast, _run, TimeResolution.Hourly, points);

        Assert.Multiple(
            () => Assert.Equal(DomainKind.Forecast, forecast.Domain),
            () => Assert.Equal(_run, forecast.ModelRun),
            () => Assert.Equal(TimeResolution.Hourly, forecast.Resolution),
            () => Assert.Equal(_run, forecast.LastValid));
    }
}
