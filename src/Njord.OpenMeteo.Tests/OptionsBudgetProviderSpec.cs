using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Polling;
using Njord.Tests.Shared;

namespace Njord.OpenMeteo.Tests;

public sealed class OptionsBudgetProviderSpec
{
    [Fact]
    public void The_rate_follows_the_free_tier_limit_without_an_override()
    {
        var provider = new OptionsBudgetProvider(Options.Create(AppSettingsDefaults.Options<PollingOptions>()));

        Assert.Equal(new BudgetRate(CostPerMinute: 480, MaxBurst: 16), provider.GetCurrentRate());
    }

    [Fact]
    public void A_budget_override_scales_the_rate_to_eighty_percent_of_the_per_minute_limit()
    {
        var provider = new OptionsBudgetProvider(Options.Create(
            AppSettingsDefaults.Options<PollingOptions>(o => o.BudgetOverride = new RequestBudget(300_000, 100))));

        Assert.Equal(80, provider.GetCurrentRate().CostPerMinute);
    }
}
