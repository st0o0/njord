using Microsoft.Extensions.Configuration;
using Njord.Core.Configuration.Polling;
using Njord.Core.Domain;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Weather;

namespace Njord.Core.Tests.Configuration.Polling;

public sealed class PollingOptionsValidatorSpec
{
    private static PollingOptionsValidator ValidatorFor(params (string Key, string Value)[] scope) =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(scope.ToDictionary(s => s.Key, s => (string?)s.Value))
            .Build(),
            new ResolvedRequirements(new DomainRequirements(Weather: WeatherParams.Temperature, AirQuality: AirQualityParams.Pm25)));

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
            ("Njord:Locations:0:Name", "Lucerne"),
            ("Njord:Models:0", "icon_d2"));
        var options = new PollingOptions
        {
            PollInterval = TimeSpan.FromMinutes(1),
            BudgetOverride = new RequestBudget(10_000, 600),
        };

        var result = validator.Validate(null, options);

        Assert.Contains("exceeds the guard of 8000", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_skip_the_budget_guard_when_the_scope_is_not_configured()
    {
        var options = new PollingOptions { PollInterval = TimeSpan.FromMinutes(1) };

        Assert.True(ValidatorFor().Validate(null, options).Succeeded);
    }
}
