using Njord.Core.Configuration.Health;
using Njord.Tests.Shared;

namespace Njord.Core.Tests.Configuration;

public sealed class OptionsValidatorsSpec
{
    [Fact]
    public void Should_reject_a_poll_overdue_factor_of_one_or_less()
    {
        var result = new HealthOptionsValidator().Validate(null, new HealthOptions { PollOverdueFactor = 1 });

        Assert.Contains("PollOverdueFactor must be greater than 1", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_reject_a_stalled_factor_that_does_not_exceed_the_overdue_factor()
    {
        var options = new HealthOptions { PollOverdueFactor = 2, PollStalledFactor = 2 };

        var result = new HealthOptionsValidator().Validate(null, options);

        Assert.Contains("PollStalledFactor must be greater than PollOverdueFactor", ValidationAssert.SingleFailure(result));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Should_reject_a_budget_degraded_percentage_on_the_boundary(double percent)
    {
        var result = new HealthOptionsValidator().Validate(null, new HealthOptions { BudgetDegradedBelowPercent = percent });

        Assert.Contains("BudgetDegradedBelowPercent", ValidationAssert.SingleFailure(result));
    }
}
