using Njord.Core;
using Njord.Core.AirQuality;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Core.Flood;
using Njord.Core.Forecast;
using Njord.Core.Marine;
using Njord.Core.Polling;
using Njord.Core.Seasonal;
using Njord.Core.Solar;
using Njord.Messages.Results;
using Njord.Polling.Configuration;

namespace Njord.Polling.Tests;

public sealed class BudgetGuardTests
{
    private static NjordOptions Scope(int locations = 1) => new()
    {
        Locations = [.. Enumerable.Range(0, locations).Select(i => new LocationOptions { Name = $"L{i}" })],
        ResolvedLocations = [.. Enumerable.Range(0, locations).Select(i => new Location($"L{i}", 0, 0))],
    };

    private static DomainRequirements Resolved(ForecastParams weather = ForecastParams.Temperature) =>
        new(Forecast: weather, AirQuality: AirQualityParams.Pm25);

    private static string? Violation(
        NjordOptions scope, PollingOptions polling, DomainRequirements requirements, DomainCallConfig? callConfig = null) =>
        BudgetGuard.Violation(scope, polling, requirements, callConfig ?? new DomainCallConfig());

    private static PollingOptions WithMonthlyBudget(int perMonth) => new() { Budget = new RequestBudget(perMonth, 600) };

    [Fact]
    public void Should_accept_usage_within_eighty_percent_of_the_budget()
    {
        Assert.Null(Violation(Scope(), new PollingOptions(), Resolved()));
    }

    [Fact]
    public void Should_report_usage_above_eighty_percent_of_the_budget()
    {
        // forecast 1 var x 4/14 + air quality 1 var x 5/14 = 0.643 per cycle x 720 = 463 calls; guard 448 of 560.
        var message = Violation(Scope(), WithMonthlyBudget(560), Resolved());

        Assert.NotNull(message);
        Assert.Contains("guard of 448", message);
        Assert.Null(Violation(Scope(), WithMonthlyBudget(600), Resolved()));
    }

    [Fact]
    public void Should_multiply_forecast_weight_by_the_configured_model_count()
    {
        // 0.286 x 3 models + 0.357 = 1.214 x 720 = 874; guard 800 of 1000. One model: 463.
        Assert.Multiple(
            () => Assert.Null(Violation(Scope(), WithMonthlyBudget(1_000), Resolved())),
            () => Assert.NotNull(Violation(Scope(), WithMonthlyBudget(1_000), Resolved(), new DomainCallConfig { ForecastModelCount = 3 })));
    }

    [Fact]
    public void Should_use_the_ensemble_forecast_days_for_the_ensemble_weight()
    {
        var resolved = new DomainRequirements(
            Forecast: ForecastParams.Temperature,
            Ensemble: EnsembleParams.TemperatureSpread);

        // forecast 0.286 + ensemble spread 1 var x 16/14 = 1.429 x 720 = 1029; guard 960 of 1200. 4 days: 411.
        Assert.Multiple(
            () => Assert.Null(Violation(Scope(), WithMonthlyBudget(1_200), resolved)),
            () => Assert.NotNull(Violation(Scope(), WithMonthlyBudget(1_200), resolved, new DomainCallConfig { EnsembleDays = 16 })));
    }

    [Fact]
    public void Should_weigh_the_satellite_domain_by_past_hours_of_a_two_week_window()
    {
        var resolved = new DomainRequirements(Satellite: SatelliteParams.GlobalTilted);

        // 2 locations x 1 var x 6/336 x 720 = 26 calls (guard 24 of 30); 48 past hours: 206 calls (guard 200 of 250).
        Assert.Multiple(
            () => Assert.NotNull(Violation(Scope(2), WithMonthlyBudget(30), resolved)),
            () => Assert.Null(Violation(Scope(2), WithMonthlyBudget(40), resolved)),
            () => Assert.NotNull(Violation(Scope(2), WithMonthlyBudget(250), resolved, new DomainCallConfig { SatellitePastHours = 48 })),
            () => Assert.Null(Violation(Scope(2), WithMonthlyBudget(300), resolved, new DomainCallConfig { SatellitePastHours = 48 })));
    }

    [Fact]
    public void Should_weigh_the_seasonal_domain_per_month_by_its_forecast_months()
    {
        var resolved = new DomainRequirements(Seasonal: SeasonalParams.TemperatureAnomaly);

        // 2 locations x 30 requests x (1 var x 90/14 = 6.43) = 386 calls; guard 384 of 480.
        Assert.Multiple(
            () => Assert.NotNull(Violation(Scope(2), WithMonthlyBudget(480), resolved)),
            () => Assert.Null(Violation(Scope(2), WithMonthlyBudget(500), resolved)));
    }

    [Fact]
    public void Should_charge_at_least_the_documented_seasonal_estimate_per_request()
    {
        var resolved = new DomainRequirements(Seasonal: SeasonalParams.TemperatureAnomaly);
        var oneMonth = new DomainCallConfig { SeasonalMonths = 1 };

        // 1 location x 30 requests x max(30/14 = 2.14, 1.3) = 64 calls; guard 48 of 60, 72 of 90.
        Assert.Multiple(
            () => Assert.NotNull(Violation(Scope(), WithMonthlyBudget(60), resolved, oneMonth)),
            () => Assert.Null(Violation(Scope(), WithMonthlyBudget(90), resolved, oneMonth)));
    }

    [Fact]
    public void Should_charge_nothing_for_weather_when_it_is_not_required()
    {
        // Only air quality: 0.357 x 720 = 257 calls; guard 264 of 330.
        Assert.Null(Violation(Scope(), WithMonthlyBudget(330), Resolved(ForecastParams.None)));
    }

    [Fact]
    public void Should_weigh_the_flood_domain_by_its_forecast_days()
    {
        var resolved = new DomainRequirements(Forecast: ForecastParams.Temperature, Flood: FloodParams.Discharge);

        // forecast 0.286 + flood 1 var x 92/14 = 6.571 -> 4937 calls; guard 4800 of 6000. 14 days: 925.
        Assert.Multiple(
            () => Assert.NotNull(Violation(Scope(), WithMonthlyBudget(6_000), resolved)),
            () => Assert.Null(Violation(Scope(), WithMonthlyBudget(6_000), resolved, new DomainCallConfig { FloodDays = 14 })));
    }

    [Fact]
    public void Should_weigh_the_flood_ensemble_into_the_projection()
    {
        var resolved = new DomainRequirements(Forecast: ForecastParams.Temperature, Flood: FloodParams.Discharge);
        var polling = WithMonthlyBudget(1_200);

        // 14 days plain: (0.286 + 1) x 720 = 926 <= 960; ensemble: (0.286 + 1 + 4) x 720 = 3806.
        Assert.Multiple(
            () => Assert.Null(Violation(Scope(), polling, resolved, new DomainCallConfig { FloodDays = 14 })),
            () => Assert.NotNull(Violation(Scope(), polling, resolved, new DomainCallConfig { FloodDays = 14, FloodIncludeEnsemble = true })));
    }

    [Fact]
    public void Should_weigh_the_marine_domain_by_its_forecast_days()
    {
        var resolved = new DomainRequirements(Marine: MarineParams.WaveHeight);

        // 1 var x 8/14 x 720 = 411 calls; guard 400 of 500, 480 of 600.
        Assert.Multiple(
            () => Assert.NotNull(Violation(Scope(), WithMonthlyBudget(500), resolved)),
            () => Assert.Null(Violation(Scope(), WithMonthlyBudget(600), resolved)));
    }

    [Fact]
    public void Should_weigh_the_air_quality_domain_by_its_forecast_days()
    {
        // 14 days: 1 var x 14/14 x 720 = 720 calls, guard 640 of 800. 5 days: 463 calls.
        Assert.Multiple(
            () => Assert.NotNull(Violation(Scope(), WithMonthlyBudget(800), Resolved(), new DomainCallConfig { AirQualityDays = 14 })),
            () => Assert.Null(Violation(Scope(), WithMonthlyBudget(800), Resolved())));
    }

    [Fact]
    public void Should_charge_ensemble_spread_with_the_fractional_day_weight()
    {
        var resolved = new DomainRequirements(Forecast: ForecastParams.Temperature, Ensemble: EnsembleParams.TemperatureSpread);

        // forecast 0.286 + ensemble spread 0.286 = 0.571 x 720 = 411; guard 416 of 520, 400 of 500.
        Assert.Multiple(
            () => Assert.Null(Violation(Scope(), WithMonthlyBudget(520), resolved)),
            () => Assert.NotNull(Violation(Scope(), WithMonthlyBudget(500), resolved)));
    }

    [Fact]
    public void Should_charge_ensemble_models_with_full_variable_weight()
    {
        var resolved = new DomainRequirements(
            Forecast: ForecastParams.Temperature,
            Ensemble: EnsembleParams.TemperatureSpread | EnsembleParams.Models);

        // forecast 0.286 + ensemble (2 vars -> 1 x 4/14 x 3 models = 0.857) = 1.143 x 720 = 823; guard 800 of 1000. One model: 411.
        Assert.Multiple(
            () => Assert.NotNull(Violation(Scope(), WithMonthlyBudget(1_000), resolved, new DomainCallConfig { EnsembleModelCount = 3 })),
            () => Assert.Null(Violation(Scope(), WithMonthlyBudget(1_000), resolved, new DomainCallConfig { EnsembleModelCount = 1 })));
    }

    [Fact]
    public void Should_charge_nothing_for_ensemble_when_not_required()
    {
        var resolved = new DomainRequirements(Forecast: ForecastParams.Temperature);

        // forecast 0.286 x 720 = 206 calls; guard 240 of 300.
        Assert.Null(Violation(Scope(), WithMonthlyBudget(300), resolved));
    }

    [Fact]
    public void Should_weigh_the_base_weather_field_into_the_call_weight()
    {
        var tenVariables = ForecastParams.Temperature | ForecastParams.WindGusts | ForecastParams.Precipitation | ForecastParams.Humidity
            | ForecastParams.Pressure | ForecastParams.CloudCover | ForecastParams.WindSpeed | ForecastParams.Snowfall
            | ForecastParams.WindDirection | ForecastParams.ApparentTemperature;
        var plain = new DomainRequirements(Forecast: tenVariables);
        var withBase = new DomainRequirements(Forecast: tenVariables).WithBaseParams();

        // 10 variables weigh 4/14 x 720 = 206 calls; with weather_code 11 variables weigh 2 x 4/14 x 720 = 411. Guard 384 of 480.
        Assert.Multiple(
            () => Assert.Null(Violation(Scope(), WithMonthlyBudget(480), plain)),
            () => Assert.NotNull(Violation(Scope(), WithMonthlyBudget(480), withBase)));
    }
}
