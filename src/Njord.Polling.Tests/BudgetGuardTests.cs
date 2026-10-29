using System.Collections.Immutable;
using Njord.Core.AirQuality;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Core.Forecast;
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
    };

    private static FeatureRequirementsOptions Requirements(
        DomainRequirements requirements,
        ImmutableArray<string>? models = null,
        ImmutableArray<string>? ensembleModels = null) => new()
        {
            Requirements = requirements,
            WeatherModels = models ?? ["ecmwf_ifs025_ensemble_mean"],
            EnsembleModels = ensembleModels ?? [],
        };

    private static FeatureRequirementsOptions Resolved(ForecastParams weather = ForecastParams.Temperature, ImmutableArray<string>? models = null) =>
        Requirements(new DomainRequirements(Forecast: weather, AirQuality: AirQualityParams.Pm25), models);

    private static readonly ImmutableArray<string> ThreeModels = ["a", "b", "c"];

    private static string? Violation(NjordOptions scope, PollingOptions polling, FeatureRequirementsOptions requirements, FloodOptions? flood = null) =>
        BudgetGuard.Violation(scope, polling, requirements, new ForecastOptions(), flood);

    private static PollingOptions WithMonthlyBudget(int perMonth) => new() { BudgetOverride = new RequestBudget(perMonth, 600) };

    [Fact]
    public void Should_accept_usage_within_eighty_percent_of_the_budget()
    {
        // 1 location x (weather 1 + air quality 1) x 720 cycles = 1440 calls of 300000.
        Assert.Null(Violation(Scope(), new PollingOptions(), Resolved()));
    }

    [Fact]
    public void Should_report_usage_above_eighty_percent_of_the_budget()
    {
        var message = Violation(Scope(), WithMonthlyBudget(1_700), Resolved());

        Assert.NotNull(message);
        Assert.Contains("guard of 1360", message);
    }

    [Fact]
    public void Should_not_multiply_weather_weight_by_model_count()
    {
        // weather weight 1 (no model multiplier) + air quality 1 = 2 calls x 720 = 1440; 3000 budget -> within guard.
        Assert.Null(Violation(Scope(), WithMonthlyBudget(3_000), Resolved(models: ThreeModels)));
    }

    [Fact]
    public void Should_count_the_satellite_domain_once_per_poll_cycle()
    {
        var resolved = Requirements(new DomainRequirements(Satellite: SatelliteParams.GlobalTilted));

        // 2 locations x 1 call x 720 cycles = 1440 calls; 1700 budget -> guard 1360.
        Assert.NotNull(Violation(Scope(2), WithMonthlyBudget(1_700), resolved));
        Assert.Null(Violation(Scope(2), WithMonthlyBudget(1_900), resolved));
    }

    [Fact]
    public void Should_count_the_seasonal_domain_per_month_instead_of_per_poll_cycle()
    {
        var resolved = Requirements(new DomainRequirements(Seasonal: SeasonalParams.TemperatureAnomaly));

        // 2 locations x 30 calls = 60 calls; guard at 80 percent of 70 is 56.
        Assert.NotNull(Violation(Scope(2), WithMonthlyBudget(70), resolved));
        Assert.Null(Violation(Scope(2), WithMonthlyBudget(80), resolved));
    }

    [Fact]
    public void Should_charge_nothing_for_weather_when_it_is_not_required()
    {
        // Only the air quality call per cycle: 720 calls; 1000 budget -> guard 800.
        Assert.Null(Violation(Scope(), WithMonthlyBudget(1_000), Resolved(ForecastParams.None)));
    }

    [Fact]
    public void Should_weigh_the_flood_ensemble_into_the_projection()
    {
        var resolved = Requirements(new DomainRequirements(
            Forecast: ForecastParams.Temperature, Flood: Njord.Core.Flood.FloodParams.Discharge));
        var polling = WithMonthlyBudget(2_000);

        // plain: (1 + 1) x 720 = 1440 <= 1600; ensemble: (1 + 5) x 720 = 4320.
        Assert.Multiple(
            () => Assert.Null(Violation(Scope(), polling, resolved, new FloodOptions())),
            () => Assert.NotNull(Violation(Scope(), polling, resolved, new FloodOptions { IncludeEnsemble = true })));
    }

    [Fact]
    public void Should_charge_ensemble_spread_as_one_call_per_cycle()
    {
        // weather 1 + ensemble spread 1 = 2 calls x 720 = 1440; 1800 budget -> guard 1440, exactly at limit.
        var resolved = Requirements(
            new DomainRequirements(Forecast: ForecastParams.Temperature, Ensemble: EnsembleParams.TemperatureSpread),
            ensembleModels: ["ecmwf_ifs025_ensemble_mean"]);

        Assert.Null(Violation(Scope(), WithMonthlyBudget(1_800), resolved));
    }

    [Fact]
    public void Should_charge_ensemble_models_with_full_variable_weight()
    {
        // weather 1 + ensemble (1 var_weight x 3 models) = 4 calls x 720 = 2880; 3000 budget -> guard 2400, exceeds.
        var resolved = Requirements(
            new DomainRequirements(
                Forecast: ForecastParams.Temperature,
                Ensemble: EnsembleParams.TemperatureSpread | EnsembleParams.Models),
            ensembleModels: ThreeModels);

        Assert.NotNull(Violation(Scope(), WithMonthlyBudget(3_000), resolved));
    }

    [Fact]
    public void Should_charge_nothing_for_ensemble_when_not_required()
    {
        var resolved = Requirements(new DomainRequirements(Forecast: ForecastParams.Temperature));

        // weather 1 call x 720 = 720; 1000 budget -> within guard.
        Assert.Null(Violation(Scope(), WithMonthlyBudget(1_000), resolved));
    }

    [Fact]
    public void Should_weigh_the_base_weather_field_into_the_call_weight()
    {
        var tenVariables = ForecastParams.Temperature | ForecastParams.WindGusts | ForecastParams.Precipitation | ForecastParams.Humidity
            | ForecastParams.Pressure | ForecastParams.CloudCover | ForecastParams.WindSpeed | ForecastParams.Snowfall
            | ForecastParams.WindDirection | ForecastParams.ApparentTemperature;
        var plain = Requirements(new DomainRequirements(Forecast: tenVariables));
        var withBase = Requirements(FeatureResolution.WithBaseParams(new DomainRequirements(Forecast: tenVariables)));

        // 10 variables weigh 1 call x 720 cycles = 720 (guard 800 of 1000); with weather_code 11 variables weigh 2 calls = 1440.
        Assert.Multiple(
            () => Assert.Null(Violation(Scope(), WithMonthlyBudget(1_000), plain)),
            () => Assert.NotNull(Violation(Scope(), WithMonthlyBudget(1_000), withBase)));
    }
}
