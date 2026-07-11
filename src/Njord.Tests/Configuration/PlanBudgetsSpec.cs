using Njord.Configuration;

namespace Njord.Tests.Configuration;

public sealed class PlanBudgetsSpec
{
    [Fact(Timeout = 5000)]
    public void Hobby_preset_resolves_the_verified_limits()
    {
        var budget = PlanBudgets.Resolve(NjordPlan.Hobby, overrideBudget: null);

        Assert.NotNull(budget);
        Assert.Equal(20_000, budget.RequestsPerMonth);
        Assert.Equal(60, budget.RequestsPerMinute);
    }

    [Fact(Timeout = 5000)]
    public void Override_supersedes_the_preset()
    {
        var overrideBudget = new RequestBudget(50_000, 120);

        var budget = PlanBudgets.Resolve(NjordPlan.Hobby, overrideBudget);

        Assert.Equal(overrideBudget, budget);
    }

    [Fact(Timeout = 5000)]
    public void Custom_plan_without_override_resolves_to_nothing()
    {
        var budget = PlanBudgets.Resolve(NjordPlan.Custom, overrideBudget: null);

        Assert.Null(budget);
    }

    [Fact(Timeout = 5000)]
    public void Every_preset_plan_resolves_to_a_budget()
    {
        foreach (var plan in Enum.GetValues<NjordPlan>().Where(p => p != NjordPlan.Custom))
        {
            var budget = PlanBudgets.Resolve(plan, overrideBudget: null);

            Assert.NotNull(budget);
            Assert.True(budget.RequestsPerMonth > 0);
            Assert.True(budget.RequestsPerMinute > 0);
        }
    }
}
