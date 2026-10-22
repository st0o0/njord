using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Core.Polling;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Seasonal;
using Njord.Forecast.Solar;
using Njord.Forecast.Weather;
using Njord.Polling.Configuration;

namespace Njord.Polling.Tests;

public sealed class BudgetGuardSpec
{
    private static NjordOptions Scope(int locations = 1) => new()
    {
        Locations = [.. Enumerable.Range(0, locations).Select(i => new LocationOptions { Name = $"L{i}" })],
    };

    private static ResolvedRequirements Resolved(WeatherParams weather = WeatherParams.Temperature) =>
        new(new DomainRequirements(Weather: weather, AirQuality: AirQualityParams.Pm25));

    private static PollingOptions WithMonthlyBudget(int perMonth) => new() { BudgetOverride = new RequestBudget(perMonth, 600) };

    [Fact]
    public void Should_accept_usage_within_eighty_percent_of_the_budget()
    {
        // 1 location x (weather 1 + air quality 1) x 720 cycles = 1440 calls of 300000.
        Assert.Null(BudgetGuard.Violation(Scope(), new WeatherOptions(), new PollingOptions(), Resolved()));
    }

    [Fact]
    public void Should_report_usage_above_eighty_percent_of_the_budget()
    {
        var message = BudgetGuard.Violation(Scope(), new WeatherOptions(), WithMonthlyBudget(1_700), Resolved());

        Assert.NotNull(message);
        Assert.Contains("guard of 1360", message);
    }

    [Fact]
    public void Should_multiply_the_weather_weight_by_the_model_count()
    {
        // weather weight 1 x 3 models + air quality 1 = 4 calls x 720 cycles = 2880 calls; 3000 budget -> guard 2400.
        var message = BudgetGuard.Violation(Scope(), new WeatherOptions(), WithMonthlyBudget(3_000), Resolved(), weatherModelCount: 3);

        Assert.NotNull(message);
        Assert.Contains("Projected API usage of 2880", message);
        Assert.Contains("weight 3", message);
        Assert.Null(BudgetGuard.Violation(Scope(), new WeatherOptions(), WithMonthlyBudget(3_000), Resolved()));
    }

    [Fact]
    public void Should_count_the_satellite_domain_once_per_poll_cycle()
    {
        var resolved = new ResolvedRequirements(new DomainRequirements(Satellite: SatelliteParams.GlobalTilted));

        // 2 locations x 1 call x 720 cycles = 1440 calls; 1700 budget -> guard 1360.
        Assert.NotNull(BudgetGuard.Violation(Scope(2), new WeatherOptions(), WithMonthlyBudget(1_700), resolved));
        Assert.Null(BudgetGuard.Violation(Scope(2), new WeatherOptions(), WithMonthlyBudget(1_900), resolved));
    }

    [Fact]
    public void Should_count_the_seasonal_domain_per_month_instead_of_per_poll_cycle()
    {
        var resolved = new ResolvedRequirements(new DomainRequirements(Seasonal: SeasonalParams.TemperatureAnomaly));

        // 2 locations x 30 calls = 60 calls; guard at 80 percent of 70 is 56.
        Assert.NotNull(BudgetGuard.Violation(Scope(2), new WeatherOptions(), WithMonthlyBudget(70), resolved));
        Assert.Null(BudgetGuard.Violation(Scope(2), new WeatherOptions(), WithMonthlyBudget(80), resolved));
    }

    [Fact]
    public void Should_charge_nothing_for_weather_when_it_is_not_required()
    {
        // Only the air quality call per cycle: 720 calls; 1000 budget -> guard 800.
        Assert.Null(BudgetGuard.Violation(Scope(), new WeatherOptions(), WithMonthlyBudget(1_000), Resolved(WeatherParams.None)));
    }

    [Fact]
    public void Should_weigh_the_flood_ensemble_into_the_projection()
    {
        var resolved = new ResolvedRequirements(new DomainRequirements(
            Weather: WeatherParams.Temperature, Flood: Forecast.Flood.FloodParams.Discharge));
        var polling = WithMonthlyBudget(2_000);

        // plain: (1 + 1) x 720 = 1440 <= 1600; ensemble: (1 + 5) x 720 = 4320.
        Assert.Multiple(
            () => Assert.Null(BudgetGuard.Violation(Scope(), new WeatherOptions(), polling, resolved, new FloodOptions())),
            () => Assert.NotNull(BudgetGuard.Violation(Scope(), new WeatherOptions(), polling, resolved, new FloodOptions { IncludeEnsemble = true })));
    }

    [Fact]
    public void Should_weigh_the_base_weather_field_into_the_call_weight()
    {
        var tenVariables = WeatherParams.Temperature | WeatherParams.WindGusts | WeatherParams.Precipitation | WeatherParams.Humidity
            | WeatherParams.Pressure | WeatherParams.CloudCover | WeatherParams.WindSpeed | WeatherParams.Snowfall
            | WeatherParams.WindDirection | WeatherParams.ApparentTemperature;
        var plain = new ResolvedRequirements(new DomainRequirements(Weather: tenVariables));
        var withBase = new ResolvedRequirements(FeatureResolution.WithBaseParams(new DomainRequirements(Weather: tenVariables)));

        // 10 variables weigh 1 call x 720 cycles = 720 (guard 800 of 1000); with weather_code 11 variables weigh 2 calls = 1440.
        Assert.Multiple(
            () => Assert.Null(BudgetGuard.Violation(Scope(), new WeatherOptions(), WithMonthlyBudget(1_000), plain)),
            () => Assert.NotNull(BudgetGuard.Violation(Scope(), new WeatherOptions(), WithMonthlyBudget(1_000), withBase)));
    }
}
