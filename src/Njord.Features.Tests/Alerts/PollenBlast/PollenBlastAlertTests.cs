using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.AirQuality;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Alerts.PollenBlast;
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
}
