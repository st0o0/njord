using Microsoft.Extensions.Options;
using Njord.Core.AirQuality;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Alerts.PollenBlast;
using Njord.Messages.Alerts;
using Njord.Messages.Results.Alerts;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Alerts.PollenBlast;

public sealed class PollenBlastAlertTests
{
    private static IOptions<T> Opts<T>(T value) where T : class => Options.Create(value);

    private static PollenBlastAlert Rule() => new(Opts(new PollenBlastConfig { Enabled = true }));

    private static LocationForecast Forecast(DomainForecast<ForecastPoint> weather, DomainForecast<AirQualityPoint> aq) =>
        new(Lucerne, weather, aq);

    [Fact]
    public void Rain_to_dry_transition_with_rising_pollen_triggers_alert()
    {
        var rainyWeather = Day((_, p) => p with { Temperature = 20.0, WindSpeed = 3.0, Precipitation = 5.0 });
        var dryWeather = Day((_, p) => p with { Temperature = 20.0, WindSpeed = 3.0, Precipitation = 0.0 });
        var lowPollen = CleanAir(24, (_, p) => p with { BirchPollen = 5.0 });
        var highPollen = CleanAir(24, (_, p) => p with { BirchPollen = 30.0 });

        var pastRain = Forecast(rainyWeather, lowPollen);
        var pastDry = Forecast(dryWeather, lowPollen);
        var current = Forecast(dryWeather, highPollen);

        var result = Run(Rule(), Now, pastRain, pastDry, current);
        Assert.NotNull(result);
        Assert.IsType<PollenBlastAlertResult>(result, exactMatch: false);
    }

    [Fact]
    public void Continuous_dry_without_rain_history_does_not_trigger()
    {
        var dryWeather = Day((_, p) => p with { Temperature = 20.0, WindSpeed = 3.0, Precipitation = 0.0 });
        var highPollen = CleanAir(24, (_, p) => p with { BirchPollen = 30.0 });

        var past1 = Forecast(dryWeather, highPollen);
        var past2 = Forecast(dryWeather, highPollen);
        var current = Forecast(dryWeather, highPollen);

        var result = Run(Rule(), Now, past1, past2, current);
        Assert.Null(result);
    }

    [Fact]
    public void No_pollen_data_does_not_trigger()
    {
        var rainyWeather = Day((_, p) => p with { Temperature = 20.0, WindSpeed = 3.0, Precipitation = 5.0 });
        var dryWeather = Day((_, p) => p with { Temperature = 20.0, WindSpeed = 3.0, Precipitation = 0.0 });
        var noPollen = CleanAir(24);

        var past = Forecast(rainyWeather, noPollen);
        var current = Forecast(dryWeather, noPollen);

        var result = Run(Rule(), Now, past, past, current);
        Assert.Null(result);
    }

    [Fact]
    public void Very_high_pollen_after_rain_produces_red()
    {
        var rainyWeather = Day((_, p) => p with { Temperature = 20.0, WindSpeed = 3.0, Precipitation = 5.0 });
        var dryWeather = Day((_, p) => p with { Temperature = 20.0, WindSpeed = 3.0, Precipitation = 0.0 });
        var lowPollen = CleanAir(24, (_, p) => p with { GrassPollen = 5.0 });
        var extremePollen = CleanAir(24, (_, p) => p with { GrassPollen = 120.0 });

        var result = Run(Rule(), Now, Forecast(rainyWeather, lowPollen), Forecast(dryWeather, lowPollen), Forecast(dryWeather, extremePollen));
        Assert.NotNull(result);
        var alert = Assert.IsType<PollenBlastAlertResult>(result, exactMatch: false);
        Assert.Equal(AlertSeverity.Red, alert.Severity);
    }

    [Fact]
    public void Moderate_pollen_after_rain_produces_yellow()
    {
        var rainyWeather = Day((_, p) => p with { Temperature = 20.0, WindSpeed = 3.0, Precipitation = 5.0 });
        var dryWeather = Day((_, p) => p with { Temperature = 20.0, WindSpeed = 3.0, Precipitation = 0.0 });
        var lowPollen = CleanAir(24, (_, p) => p with { BirchPollen = 5.0 });
        var risePollen = CleanAir(24, (_, p) => p with { BirchPollen = 25.0 });

        var result = Run(Rule(), Now, Forecast(rainyWeather, lowPollen), Forecast(dryWeather, lowPollen), Forecast(dryWeather, risePollen));
        Assert.NotNull(result);
        var alert = Assert.IsType<PollenBlastAlertResult>(result, exactMatch: false);
        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
    }

    [Fact]
    public void Insufficient_history_does_not_trigger()
    {
        var dryWeather = Day((_, p) => p with { Temperature = 20.0, WindSpeed = 3.0, Precipitation = 0.0 });
        var highPollen = CleanAir(24, (_, p) => p with { BirchPollen = 100.0 });

        var result = Run(Rule(), Now, Forecast(dryWeather, highPollen), Forecast(dryWeather, highPollen));
        Assert.Null(result);
    }

    [Fact]
    public void Wet_current_conditions_do_not_trigger()
    {
        var rainyWeather = Day((_, p) => p with { Temperature = 20.0, WindSpeed = 3.0, Precipitation = 5.0 });
        var lowPollen = CleanAir(24, (_, p) => p with { BirchPollen = 5.0 });
        var highPollen = CleanAir(24, (_, p) => p with { BirchPollen = 100.0 });
        var wetCurrent = Day((_, p) => p with { Temperature = 20.0, WindSpeed = 3.0, Precipitation = 2.0 });

        var result = Run(Rule(), Now, Forecast(rainyWeather, lowPollen), Forecast(rainyWeather, lowPollen), Forecast(wetCurrent, highPollen));
        Assert.Null(result);
    }

    [Fact]
    public void Orange_severity_when_pollen_at_50()
    {
        var rainy = Forecast(
            Day((_, p) => p with { Temperature = 20.0, WindSpeed = 2.0, Precipitation = 2.0 }),
            CleanAir(24, (_, p) => p with { BirchPollen = 10.0, GrassPollen = 10.0 }));
        var transition = Forecast(
            Day((_, p) => p with { Temperature = 20.0, WindSpeed = 2.0, Precipitation = 0.0 }),
            CleanAir(24, (_, p) => p with { BirchPollen = 20.0, GrassPollen = 20.0 }));
        var current = Forecast(
            Day((_, p) => p with { Temperature = 20.0, WindSpeed = 2.0, Precipitation = 0.0 }),
            CleanAir(24, (_, p) => p with { BirchPollen = 50.0, GrassPollen = 50.0 }));

        var result = Run(Rule(), Now, rainy, transition, current);
        Assert.NotNull(result);
        var alert = Assert.IsType<PollenBlastAlertResult>(result, exactMatch: false);
        Assert.Equal(AlertSeverity.Orange, alert.Severity);
    }

    [Fact]
    public void Returns_null_when_pollen_delta_below_threshold()
    {
        var rainy = Forecast(
            Day((_, p) => p with { Temperature = 20.0, WindSpeed = 2.0, Precipitation = 2.0 }),
            CleanAir(24, (_, p) => p with { BirchPollen = 40.0, GrassPollen = 40.0 }));
        var transition = Forecast(
            Day((_, p) => p with { Temperature = 20.0, WindSpeed = 2.0, Precipitation = 0.0 }),
            CleanAir(24, (_, p) => p with { BirchPollen = 40.0, GrassPollen = 40.0 }));
        var current = Forecast(
            Day((_, p) => p with { Temperature = 20.0, WindSpeed = 2.0, Precipitation = 0.0 }),
            CleanAir(24, (_, p) => p with { BirchPollen = 45.0, GrassPollen = 45.0 }));

        Assert.Null(Run(Rule(), Now, rainy, transition, current));
    }

    [Fact]
    public void Returns_null_when_current_temperature_below_threshold()
    {
        var rainy = Forecast(
            Day((_, p) => p with { Temperature = 20.0, WindSpeed = 2.0, Precipitation = 2.0 }),
            CleanAir(24, (_, p) => p with { BirchPollen = 10.0 }));
        var transition = Forecast(
            Day((_, p) => p with { Temperature = 20.0, WindSpeed = 2.0, Precipitation = 0.0 }),
            CleanAir(24, (_, p) => p with { BirchPollen = 10.0 }));
        var current = Forecast(
            Day((_, p) => p with { Temperature = 14.0, WindSpeed = 2.0, Precipitation = 0.0 }),
            CleanAir(24, (_, p) => p with { BirchPollen = 80.0, GrassPollen = 80.0 }));

        Assert.Null(Run(Rule(), Now, rainy, transition, current));
    }

    [Fact]
    public void Returns_null_when_current_wind_below_threshold()
    {
        var rainy = Forecast(
            Day((_, p) => p with { Temperature = 20.0, WindSpeed = 2.0, Precipitation = 2.0 }),
            CleanAir(24, (_, p) => p with { BirchPollen = 10.0 }));
        var transition = Forecast(
            Day((_, p) => p with { Temperature = 20.0, WindSpeed = 2.0, Precipitation = 0.0 }),
            CleanAir(24, (_, p) => p with { BirchPollen = 10.0 }));
        var current = Forecast(
            Day((_, p) => p with { Temperature = 20.0, WindSpeed = 1.0, Precipitation = 0.0 }),
            CleanAir(24, (_, p) => p with { BirchPollen = 80.0, GrassPollen = 80.0 }));

        Assert.Null(Run(Rule(), Now, rainy, transition, current));
    }

    [Fact]
    public void Grass_pollen_is_used_when_higher_than_birch()
    {
        var rainy = Forecast(
            Day((_, p) => p with { Temperature = 20.0, WindSpeed = 2.0, Precipitation = 2.0 }),
            CleanAir(24, (_, p) => p with { BirchPollen = 5.0, GrassPollen = 5.0 }));
        var transition = Forecast(
            Day((_, p) => p with { Temperature = 20.0, WindSpeed = 2.0, Precipitation = 0.0 }),
            CleanAir(24, (_, p) => p with { BirchPollen = 5.0, GrassPollen = 5.0 }));
        var current = Forecast(
            Day((_, p) => p with { Temperature = 20.0, WindSpeed = 2.0, Precipitation = 0.0 }),
            CleanAir(24, (_, p) => p with { BirchPollen = 10.0, GrassPollen = 120.0 }));

        var result = Run(Rule(), Now, rainy, transition, current);
        Assert.NotNull(result);
        var alert = Assert.IsType<PollenBlastAlertResult>(result, exactMatch: false);
        Assert.Equal(AlertSeverity.Red, alert.Severity);
    }
}
