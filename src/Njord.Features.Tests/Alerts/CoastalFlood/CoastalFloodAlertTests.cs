using System.Collections.Immutable;
using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Alerts.CoastalFlood;
using Njord.Forecast;
using Njord.Forecast.Forecast;
using Njord.Forecast.Marine;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Alerts.CoastalFlood;

public sealed class CoastalFloodAlertTests
{
    private static IOptions<T> Opts<T>(T value) where T : class => Options.Create(value);

    private static CoastalFloodAlert Rule() => new(Opts(new CoastalFloodConfig { Enabled = true }));

    private static DomainForecast<MarinePoint> MarineDay(double seaLevel = 0.0, double waveHeight = 0.5)
    {
        var points = Enumerable.Range(0, 24)
            .Select(h => new MarinePoint(Now.AddHours(h), WaveHeight: waveHeight, SeaLevelHeight: seaLevel, WaveDirection: 180))
            .ToImmutableArray();
        return new DomainForecast<MarinePoint>(DomainKind.Marine, Now, TimeResolution.Hourly, points);
    }

    [Fact]
    public void High_sea_level_plus_waves_produces_red()
    {
        var weather = Day((_, p) => p with { WindSpeed = 10.0, WindDirection = 180 });
        var marine = MarineDay(seaLevel: 1.0, waveHeight: 3.0);
        var forecast = new LocationForecast(Lucerne, weather, Marine: marine, Ensemble: EnsembleDay());

        var result = Run(Rule(), Now, forecast);
        Assert.NotNull(result);
        var alert = Assert.IsType<CoastalFloodAlertResult>(result, exactMatch: false);
        Assert.Equal(AlertSeverity.Red, alert.Severity);
    }

    [Fact]
    public void Sea_level_alone_produces_yellow()
    {
        var weather = Day((_, p) => p with { WindSpeed = 1.0, WindDirection = 90 });
        var marine = MarineDay(seaLevel: 0.35, waveHeight: 0.3);
        var forecast = new LocationForecast(Lucerne, weather, Marine: marine, Ensemble: EnsembleDay());

        var result = Run(Rule(), Now, forecast);
        Assert.NotNull(result);
        var alert = Assert.IsType<CoastalFloodAlertResult>(result, exactMatch: false);
        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
    }

    [Fact]
    public void Normal_sea_level_produces_no_alert()
    {
        var weather = Day((_, p) => p with { WindSpeed = 2.0, WindDirection = 0 });
        var marine = MarineDay(seaLevel: 0.1, waveHeight: 0.5);
        var forecast = new LocationForecast(Lucerne, weather, Marine: marine, Ensemble: EnsembleDay());

        var result = Run(Rule(), Now, forecast);
        Assert.Null(result);
    }

    [Fact]
    public void Missing_marine_data_skips_alert()
    {
        var weather = Day((_, p) => p with { WindSpeed = 10.0 });
        var forecast = new LocationForecast(Lucerne, weather, Ensemble: EnsembleDay());

        var result = Run(Rule(), Now, forecast);
        Assert.Null(result);
    }
}
