using Microsoft.Extensions.Options;
using Njord.Core.Configuration.Polling;
using Njord.Tests.Shared;

namespace Njord.OpenMeteo.Tests;

public sealed class OptionsBudgetProviderSpec
{
    [Fact]
    public void The_rate_follows_the_free_tier_without_an_override()
    {
        var provider = new OptionsBudgetProvider(Options.Create(AppSettingsDefaults.Options<PollingOptions>()));

        var rate = provider.GetCurrentRate();

        Assert.Equal(480, rate.CostPerMinute);
        Assert.Equal(16, rate.MaxBurst);
    }

    [Fact]
    public void A_budget_override_sets_the_rate()
    {
        var provider = new OptionsBudgetProvider(Options.Create(
            AppSettingsDefaults.Options<PollingOptions>(o => o.BudgetOverride = new RequestBudget(300_000, 100))));

        Assert.Equal(80, provider.GetCurrentRate().CostPerMinute);
    }
}
