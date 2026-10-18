using Njord.Core.Configuration.Polling;
using Njord.Tests.Shared;

namespace Njord.Core.Tests.Configuration.Polling;

public sealed class RequestBudgetSpec
{
    [Fact]
    public void Effective_budget_returns_override_when_set()
    {
        var options = new PollingOptions
        {
            BudgetOverride = new RequestBudget(5_000, 10),
        };

        Assert.Equal(5_000, BudgetCalculator.GetEffectiveBudget(options).RequestsPerMonth);
        Assert.Equal(10, BudgetCalculator.GetEffectiveBudget(options).RequestsPerMinute);
    }

    [Fact]
    public void Effective_budget_returns_free_tier_when_no_override()
    {
        var options = AppSettingsDefaults.Options<PollingOptions>();

        Assert.Equal(RequestBudget.OpenMeteoFreeTier, BudgetCalculator.GetEffectiveBudget(options));
    }
}
