using System.Collections.Immutable;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Core.Polling;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Weather;
using Njord.Polling.Configuration;
using Njord.Tests.Shared;

namespace Njord.Polling.Tests;

public sealed class PollingOptionsValidatorSpec
{
    private static PollingOptionsValidator ValidatorFor(params (string Key, string Value)[] scope) =>
        ValidatorWith(null, scope);

    private static PollingOptionsValidator ValidatorWith(ImmutableArray<string>? models, params (string Key, string Value)[] scope) =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(scope.ToDictionary(s => s.Key, s => (string?)s.Value))
            .Build(),
            Options.Create(new FeatureRequirementsOptions
            {
                Requirements = new DomainRequirements(Weather: WeatherParams.Temperature, AirQuality: AirQualityParams.Pm25),
                WeatherModels = models ?? ["ecmwf_ifs025_ensemble_mean"],
            }));

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
        var validator = ValidatorFor(
            ("Njord:Locations:0:Name", "Lucerne"));
        var options = new PollingOptions
        {
            PollInterval = TimeSpan.FromMinutes(1),
            BudgetOverride = new RequestBudget(10_000, 600),
        };

        var result = validator.Validate(null, options);

        Assert.Contains("exceeds the guard of 8000", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_count_every_weather_model_of_the_fetch_plan()
    {
        var options = new PollingOptions { BudgetOverride = new RequestBudget(2_500, 600) };
        (string, string)[] scope =
        [
            ("Njord:Locations:0:Name", "Lucerne"),
        ];

        var withoutComparison = ValidatorWith(null, scope).Validate(null, options);
        var withComparison = ValidatorWith(ImmutableArray.Create("a", "b", "c"), scope).Validate(null, options);

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
