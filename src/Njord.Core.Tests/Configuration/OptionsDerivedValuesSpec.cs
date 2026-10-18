using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;
using Njord.Tests.Shared;

namespace Njord.Core.Tests.Configuration;

public sealed class OptionsDerivedValuesSpec
{
    [Fact]
    public void Resolve_produces_the_set_of_the_parameter_registry()
    {
        var parameters = new ParameterOptions { Groups = ["Weather"], Extra = [], Exclude = ["snowfall"] };

        var resolved = parameters.Resolve();
        var expected = ParameterSelection.Resolve(["Weather"], [], ["snowfall"]);

        Assert.Multiple(
            () => Assert.Equal(expected.Hourly, resolved.Hourly),
            () => Assert.Equal(expected.Daily, resolved.Daily),
            () => Assert.DoesNotContain(resolved.Hourly, p => p.ApiName == "snowfall"));
    }

    [Fact]
    public void Resolve_reports_unknown_groups()
    {
        var parameters = new ParameterOptions { Groups = ["Nonsense"] };

        var ex = Assert.Throws<ParameterResolutionException>(() => parameters.Resolve());

        Assert.Contains(ex.Errors, e => e.Contains("Unknown parameter group: 'Nonsense'"));
    }

    [Fact]
    public void The_budget_allowance_is_the_free_tier_limits_by_default()
    {
        var allowance = AppSettingsDefaults.Options<PollingOptions>().ToBudgetAllowance();

        Assert.Equal(new BudgetAllowance(RequestBudget.OpenMeteoFreeTierDailyLimit, 300_000), allowance);
    }

    [Fact]
    public void The_budget_allowance_follows_a_budget_override()
    {
        var allowance = AppSettingsDefaults.Options<PollingOptions>(o => o.BudgetOverride = new RequestBudget(50_000, 60)).ToBudgetAllowance();

        Assert.Equal(new BudgetAllowance(RequestBudget.OpenMeteoFreeTierDailyLimit, 50_000), allowance);
    }
}
