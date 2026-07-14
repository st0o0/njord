using Njord.Configuration;

namespace Njord.Tests.Configuration;

public sealed class RequestBudgetSpec
{
    [Fact(Timeout = 5000)]
    public void Free_tier_has_documented_soft_limits()
    {
        var budget = RequestBudget.OpenMeteoFreeTier;

        Assert.Equal(300_000, budget.RequestsPerMonth);
        Assert.Equal(600, budget.RequestsPerMinute);
    }

    [Fact(Timeout = 5000)]
    public void Custom_budget_preserves_values()
    {
        var budget = new RequestBudget(10_000, 30);

        Assert.Equal(10_000, budget.RequestsPerMonth);
        Assert.Equal(30, budget.RequestsPerMinute);
    }

    [Fact(Timeout = 5000)]
    public void Effective_budget_returns_override_when_set()
    {
        var options = new NjordOptions
        {
            BudgetOverride = new RequestBudget(5_000, 10),
        };

        Assert.Equal(5_000, options.EffectiveBudget.RequestsPerMonth);
        Assert.Equal(10, options.EffectiveBudget.RequestsPerMinute);
    }

    [Fact(Timeout = 5000)]
    public void Effective_budget_returns_free_tier_when_no_override()
    {
        var options = new NjordOptions();

        Assert.Equal(RequestBudget.OpenMeteoFreeTier, options.EffectiveBudget);
    }
}
