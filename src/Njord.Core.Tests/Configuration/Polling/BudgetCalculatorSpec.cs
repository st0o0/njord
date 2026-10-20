using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;
using Njord.Core.Domain;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Seasonal;
using Njord.Forecast.Solar;
using Njord.Forecast.Weather;

namespace Njord.Core.Tests.Configuration.Polling;

public sealed class BudgetCalculatorSpec
{
    private static NjordOptions Scope(int locations = 1) => new()
    {
        Locations = [.. Enumerable.Range(0, locations).Select(i => new LocationOptions { Name = $"L{i}" })],
    };

    private static readonly WeatherParams Eleven =
        WeatherParams.Temperature | WeatherParams.ApparentTemperature | WeatherParams.WindSpeed | WeatherParams.Precipitation
        | WeatherParams.PrecipitationProbability | WeatherParams.CloudCover | WeatherParams.Pressure | WeatherParams.WeatherCode
        | WeatherParams.Humidity | WeatherParams.DewPoint | WeatherParams.UvIndex;

    private static ResolvedRequirements Resolved(WeatherParams weather = WeatherParams.Temperature) =>
        new(new DomainRequirements(Weather: weather, AirQuality: AirQualityParams.Pm25));

    [Fact]
    public void Should_project_calls_from_locations_domains_cycles_and_weight()
    {
        var scope = Scope(locations: 2);
        var polling = new PollingOptions { PollInterval = TimeSpan.FromHours(1) };

        var result = BudgetCalculator.Validate(scope, new WeatherOptions(), polling, Resolved());

        // 2 locations x (weather weight 1 + 1 other domain) = 4 calls per cycle, 720 cycles in a 30-day month.
        Assert.Multiple(
            () => Assert.Equal(4, result.DomainCallsPerCycle),
            () => Assert.Equal(4L * 720, result.ProjectedMonthlyCalls),
            () => Assert.Equal(300_000, result.MonthlyLimit),
            () => Assert.True(result.WithinBudget));
    }

    [Fact]
    public void Should_count_the_satellite_domain_once_per_poll_cycle()
    {
        var scope = Scope(locations: 2);
        var polling = new PollingOptions { PollInterval = TimeSpan.FromHours(1) };
        var resolved = new ResolvedRequirements(new DomainRequirements(Satellite: SatelliteParams.GlobalTilted));

        var result = BudgetCalculator.Validate(scope, new WeatherOptions(), polling, resolved);

        Assert.Multiple(
            () => Assert.Equal(2, result.DomainCallsPerCycle),
            () => Assert.Equal(2L * 720, result.ProjectedMonthlyCalls));
    }

    [Fact]
    public void Should_count_the_seasonal_domain_once_a_day_instead_of_once_per_poll_cycle()
    {
        var scope = Scope(locations: 2);
        var resolved = new ResolvedRequirements(new DomainRequirements(Seasonal: SeasonalParams.TemperatureAnomaly));

        var result = BudgetCalculator.Validate(scope, new WeatherOptions(), new PollingOptions(), resolved);

        Assert.Multiple(
            () => Assert.Equal(0, result.DomainCallsPerCycle),
            () => Assert.Equal(2L * 30, result.ProjectedMonthlyCalls));
    }

    [Fact]
    public void Should_weigh_the_resolved_weather_variables_and_forecast_days_into_the_projection()
    {
        var small = BudgetCalculator.Validate(Scope(), new WeatherOptions(), new PollingOptions(), Resolved());
        var large = BudgetCalculator.Validate(Scope(), new WeatherOptions(), new PollingOptions(), Resolved(Eleven));
        var longRange = BudgetCalculator.Validate(Scope(), new WeatherOptions { ForecastDays = 15 }, new PollingOptions(), Resolved());

        Assert.Multiple(
            () => Assert.Equal(1, small.ApiCallWeight),
            () => Assert.Equal(2, large.ApiCallWeight),
            () => Assert.Equal(2, longRange.ApiCallWeight));
    }

    [Fact]
    public void Should_count_optional_weather_variables_toward_the_weight()
    {
        var optionalOnly = new ResolvedRequirements(new DomainRequirements(
            Weather: WeatherParams.Temperature, OptionalWeather: Eleven));

        var result = BudgetCalculator.Validate(Scope(), new WeatherOptions(), new PollingOptions(), optionalOnly);

        Assert.Equal(2, result.ApiCallWeight);
    }

    [Fact]
    public void Should_charge_the_weather_weight_to_the_weather_domain_only()
    {
        var polling = new PollingOptions { PollInterval = TimeSpan.FromHours(1) };

        var result = BudgetCalculator.Validate(Scope(), new WeatherOptions(), polling, Resolved(Eleven));

        // weather weight 2 + 1 air quality call per cycle, 720 cycles.
        Assert.Equal(3L * 720, result.ProjectedMonthlyCalls);
    }

    [Fact]
    public void Should_charge_nothing_for_weather_when_it_is_not_required()
    {
        var polling = new PollingOptions { PollInterval = TimeSpan.FromHours(1) };

        var result = BudgetCalculator.Validate(Scope(), new WeatherOptions(), polling, Resolved(WeatherParams.None));

        Assert.Equal(720L, result.ProjectedMonthlyCalls);
    }

    [Theory]
    [InlineData(10, 4, 1)]
    [InlineData(11, 4, 2)]
    [InlineData(31, 4, 4)]
    [InlineData(10, 15, 2)]
    [InlineData(11, 30, 6)]
    public void Should_weigh_one_call_per_ten_variables_and_fourteen_days(int variables, int days, int weight)
    {
        Assert.Equal(weight, BudgetCalculator.ComputeWeight(variables, days));
    }

    [Fact]
    public void Should_use_the_override_as_the_effective_budget()
    {
        var polling = new PollingOptions { BudgetOverride = new RequestBudget(1_000, 10) };

        Assert.Equal(new RequestBudget(1_000, 10), BudgetCalculator.GetEffectiveBudget(polling));
    }

    [Fact]
    public void Should_fall_back_to_the_open_meteo_free_tier()
    {
        Assert.Equal(RequestBudget.OpenMeteoFreeTier, BudgetCalculator.GetEffectiveBudget(new PollingOptions()));
    }

    [Fact]
    public void Should_warn_between_eighty_and_one_hundred_percent_of_the_budget()
    {
        var polling = new PollingOptions { BudgetOverride = new RequestBudget(1_700, 600) };

        var result = BudgetCalculator.Validate(Scope(), new WeatherOptions(), polling, Resolved());

        Assert.Multiple(
            () => Assert.InRange(result.UsagePercent, 80.0, 100.0),
            () => Assert.True(result.WithinBudget),
            () => Assert.Single(result.Warnings));
    }

    [Fact]
    public void Should_flag_usage_above_the_budget_as_not_within_budget()
    {
        var polling = new PollingOptions { BudgetOverride = new RequestBudget(100, 600) };

        var result = BudgetCalculator.Validate(Scope(), new WeatherOptions(), polling, Resolved());

        Assert.False(result.WithinBudget);
    }

    [Fact]
    public void Should_only_report_a_guard_violation_above_eighty_percent()
    {
        var polling = new PollingOptions { BudgetOverride = new RequestBudget(100, 600) };
        var heavy = BudgetCalculator.Validate(Scope(), new WeatherOptions(), polling, Resolved());
        var light = BudgetCalculator.Validate(Scope(), new WeatherOptions(), new PollingOptions(), Resolved());

        Assert.Multiple(
            () => Assert.Null(BudgetCalculator.GuardViolation(light, TimeSpan.FromHours(1))),
            () => Assert.Contains("guard of 80", BudgetCalculator.GuardViolation(heavy, TimeSpan.FromHours(1))));
    }

    [Fact]
    public void Should_weigh_the_flood_ensemble_into_the_projection()
    {
        var resolved = new ResolvedRequirements(new DomainRequirements(
            Weather: WeatherParams.Temperature, Flood: Njord.Forecast.Flood.FloodParams.Discharge));

        var plain = BudgetCalculator.Validate(Scope(), new WeatherOptions(), new PollingOptions(), resolved, new FloodOptions());
        var ensemble = BudgetCalculator.Validate(Scope(), new WeatherOptions(), new PollingOptions(), resolved, new FloodOptions { IncludeEnsemble = true });
        var noFlood = BudgetCalculator.Validate(Scope(), new WeatherOptions(), new PollingOptions(), Resolved(), new FloodOptions { IncludeEnsemble = true });

        Assert.Multiple(
            () => Assert.Equal(2L * 720, plain.ProjectedMonthlyCalls),
            () => Assert.Equal((1L + BudgetCalculator.FloodEnsembleWeight) * 720, ensemble.ProjectedMonthlyCalls),
            () => Assert.Equal(2L * 720, noFlood.ProjectedMonthlyCalls));
    }
}
