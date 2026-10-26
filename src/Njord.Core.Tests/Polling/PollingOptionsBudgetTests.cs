using Njord.Core.Configuration;
using Njord.Core.Polling;

namespace Njord.Core.Tests.Polling;

public sealed class PollingOptionsBudgetTests
{
    [Fact]
    public void Should_use_the_override_as_the_effective_budget()
    {
        var polling = new PollingOptions { BudgetOverride = new RequestBudget(1_000, 10) };

        Assert.Equal(new RequestBudget(1_000, 10), polling.EffectiveBudget);
    }

    [Fact]
    public void Should_fall_back_to_the_open_meteo_free_tier()
    {
        Assert.Equal(RequestBudget.OpenMeteoFreeTier, new PollingOptions().EffectiveBudget);
    }
}
