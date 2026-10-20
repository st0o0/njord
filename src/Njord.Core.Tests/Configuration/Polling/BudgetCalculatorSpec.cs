using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;

namespace Njord.Core.Tests.Configuration.Polling;

public sealed class BudgetCalculatorSpec
{
    private static NjordOptions Scope(int locations = 1) => new()
    {
        Locations = [.. Enumerable.Range(0, locations).Select(i => new LocationOptions { Name = $"L{i}" })],
        Models = ["icon_d2"],
    };

    [Fact]
    public void Should_project_calls_from_locations_domains_cycles_and_weight()
    {
        var scope = Scope(locations: 2);
        var weight = scope.Parameters.Resolve().ApiCallWeight;
        var polling = new PollingOptions { PollInterval = TimeSpan.FromHours(1) };

        var result = BudgetCalculator.Validate(scope, polling, 2);

        // 2 locations x 2 domains = 4 calls per cycle, 720 cycles in a 30-day month.
        Assert.Multiple(
            () => Assert.Equal(4, result.DomainCallsPerCycle),
            () => Assert.Equal(4L * 720 * weight, result.ProjectedMonthlyCalls),
            () => Assert.Equal(300_000, result.MonthlyLimit),
            () => Assert.True(result.WithinBudget));
    }

    [Fact]
    public void Should_use_the_override_as_the_effective_budget()
    {
        var polling = new PollingOptions { BudgetOverride = new RequestBudget(1_000, 10) };

        Assert.Equal(new RequestBudget(1_000, 10), BudgetCalculator.GetEffectiveBudget(polling));
    }

    [Fact]
    public void Should_fall_back_to_the_open_meteo_free_tier()
    {
        Assert.Equal(RequestBudget.OpenMeteoFreeTier, BudgetCalculator.GetEffectiveBudget(new PollingOptions()));
    }

    [Fact]
    public void Should_warn_between_eighty_and_one_hundred_percent_of_the_budget()
    {
        var polling = new PollingOptions { BudgetOverride = new RequestBudget(7_000, 600) };

        var result = BudgetCalculator.Validate(Scope(), polling, 2);

        Assert.Multiple(
            () => Assert.InRange(result.UsagePercent, 80.0, 100.0),
            () => Assert.True(result.WithinBudget),
            () => Assert.Single(result.Warnings));
    }

    [Fact]
    public void Should_flag_usage_above_the_budget_as_not_within_budget()
    {
        var polling = new PollingOptions { BudgetOverride = new RequestBudget(100, 600) };

        var result = BudgetCalculator.Validate(Scope(), polling, 2);

        Assert.False(result.WithinBudget);
    }

    [Fact]
    public void Should_only_report_a_guard_violation_above_eighty_percent()
    {
        var polling = new PollingOptions { BudgetOverride = new RequestBudget(100, 600) };
        var heavy = BudgetCalculator.Validate(Scope(), polling, 2);
        var light = BudgetCalculator.Validate(Scope(), new PollingOptions(), 2);

        Assert.Multiple(
            () => Assert.Null(BudgetCalculator.GuardViolation(light, TimeSpan.FromHours(1))),
            () => Assert.Contains("guard of 80", BudgetCalculator.GuardViolation(heavy, TimeSpan.FromHours(1))));
    }
}
