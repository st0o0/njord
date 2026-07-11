using Njord.Configuration;

namespace Njord.Tests.Configuration;

public sealed class NjordOptionsSpec
{
    [Fact(Timeout = 5000)]
    public void Without_an_override_the_budget_is_the_open_meteo_free_tier()
    {
        var options = new NjordOptions();

        Assert.Equal(300_000, options.EffectiveBudget.RequestsPerMonth);
        Assert.Equal(600, options.EffectiveBudget.RequestsPerMinute);
    }

    [Fact(Timeout = 5000)]
    public void An_explicit_override_supersedes_the_default()
    {
        var overrideBudget = new RequestBudget(50_000, 60);

        var options = new NjordOptions { BudgetOverride = overrideBudget };

        Assert.Equal(overrideBudget, options.EffectiveBudget);
    }
}
