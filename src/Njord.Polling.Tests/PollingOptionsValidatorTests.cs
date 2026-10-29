using Microsoft.Extensions.Options;
using Njord.Core.AirQuality;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Core.Polling;
using Njord.Polling.Configuration;
using Njord.Tests.Shared;

namespace Njord.Polling.Tests;

public sealed class PollingOptionsValidatorTests
{
    private static PollingOptionsValidator ValidatorFor(int locations = 0) =>
        ValidatorWith(null, locations);

    private static PollingOptionsValidator ValidatorWith(string[]? ensembleModels, int locations)
    {
        var hasEnsemble = ensembleModels is { Length: > 0 };
        var scope = new NjordOptions
        {
            Locations = [.. Enumerable.Range(0, locations).Select(i => new LocationOptions { Name = $"L{i}" })],
        };
        return new(
            Options.Create(scope),
            Options.Create(new ForecastOptions()),
            Options.Create(new EnsembleOptions { Models = ensembleModels ?? [] }),
            Options.Create(new FloodOptions()),
            Options.Create(new DomainRequirements(
                Forecast: ForecastParams.Temperature,
                AirQuality: AirQualityParams.Pm25,
                Ensemble: hasEnsemble ? EnsembleParams.TemperatureSpread | EnsembleParams.Models : EnsembleParams.None)));
    }

    [Theory]
    [InlineData("00:00:00")]
    [InlineData("-00:05:00")]
    public void Should_reject_a_non_positive_poll_interval(string interval)
    {
        var options = new PollingOptions { PollInterval = TimeSpan.Parse(interval) };

        var result = ValidatorFor().Validate(null, options);

        Assert.Equal("PollInterval must be positive.", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_reject_a_non_positive_discovery_interval()
    {
        var result = ValidatorFor().Validate(null, new PollingOptions { DiscoveryInterval = TimeSpan.Zero });

        Assert.Equal("DiscoveryInterval must be positive.", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_reject_a_budget_override_with_a_zero_limit()
    {
        var options = new PollingOptions { BudgetOverride = new RequestBudget(0, 600) };

        var result = ValidatorFor().Validate(null, options);

        Assert.Contains("BudgetOverride", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_reject_polling_that_projects_beyond_eighty_percent_of_the_budget()
    {
        var validator = ValidatorFor(locations: 1);
        var options = new PollingOptions
        {
            PollInterval = TimeSpan.FromMinutes(1),
            Budget = new RequestBudget(10_000, 600),
        };

        var result = validator.Validate(null, options);

        Assert.Contains("exceeds the guard of 8000", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_count_ensemble_models_in_the_budget_projection()
    {
        var options = new PollingOptions { Budget = new RequestBudget(2_500, 600) };

        var withoutComparison = ValidatorWith(null, 1).Validate(null, options);
        var withComparison = ValidatorWith(["a", "b", "c"], 1).Validate(null, options);

        Assert.Multiple(
            () => Assert.True(withoutComparison.Succeeded),
            () => Assert.Contains("exceeds the guard of 2000", ValidationAssert.SingleFailure(withComparison)));
    }

    [Fact]
    public void Should_skip_the_budget_guard_when_the_scope_is_not_configured()
    {
        var options = new PollingOptions { PollInterval = TimeSpan.FromMinutes(1) };

        Assert.True(ValidatorFor().Validate(null, options).Succeeded);
    }
}
