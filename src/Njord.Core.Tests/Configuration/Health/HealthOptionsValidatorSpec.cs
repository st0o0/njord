using Njord.Core.Configuration.Health;
using Njord.Tests.Shared;

namespace Njord.Core.Tests.Configuration.Health;

public sealed class HealthOptionsValidatorSpec
{
    private static readonly HealthOptionsValidator Validator = new();

    [Fact]
    public void The_shipped_defaults_are_valid()
    {
        var result = Validator.Validate(null, AppSettingsDefaults.Options<HealthOptions>());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void An_overdue_factor_of_one_or_less_fails()
    {
        var result = Validator.Validate(null, AppSettingsDefaults.Options<HealthOptions>(o => o.PollOverdueFactor = 1));

        Assert.Equal(["Health.PollOverdueFactor must be greater than 1, got 1"], result.Failures);
    }

    [Fact]
    public void A_stalled_factor_not_above_the_overdue_factor_fails()
    {
        var result = Validator.Validate(null, AppSettingsDefaults.Options<HealthOptions>(o => o.PollStalledFactor = 2));

        Assert.Equal(["Health.PollStalledFactor must be greater than PollOverdueFactor (2), got 2"], result.Failures);
    }

    [Fact]
    public void A_non_positive_mqtt_threshold_fails()
    {
        var result = Validator.Validate(null, AppSettingsDefaults.Options<HealthOptions>(o => o.MqttUnhealthyAfter = TimeSpan.Zero));

        Assert.Equal(["Health.MqttUnhealthyAfter must be positive, got 00:00:00"], result.Failures);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void A_degraded_percentage_outside_the_open_range_fails(double percent)
    {
        var result = Validator.Validate(null, AppSettingsDefaults.Options<HealthOptions>(o => o.BudgetDegradedBelowPercent = percent));

        Assert.Equal(
            [$"Health.BudgetDegradedBelowPercent must be between 0 and 100 exclusive, got {percent}"],
            result.Failures);
    }
}
