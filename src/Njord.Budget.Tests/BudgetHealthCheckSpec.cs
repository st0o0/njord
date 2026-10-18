using Microsoft.Extensions.Diagnostics.HealthChecks;
using Njord.Budget.Health;
using Njord.Core.Configuration.Polling;

namespace Njord.Budget.Tests;

public sealed class BudgetHealthCheckSpec
{
    private static readonly BudgetAllowance Allowance = new(10_000, 300_000);

    [Fact]
    public void Plenty_of_budget_remaining_is_healthy()
    {
        var result = BudgetHealthCheck.Evaluate(new BudgetUsage(100, 1_000), Allowance, 5);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public void Less_than_the_configured_percentage_of_the_month_remaining_is_degraded()
    {
        var result = BudgetHealthCheck.Evaluate(new BudgetUsage(100, 288_000), Allowance, 5);

        Assert.Multiple(
            () => Assert.Equal(HealthStatus.Degraded, result.Status),
            () => Assert.Equal("Monthly request budget low (4.0% remaining)", result.Description));
    }

    [Fact]
    public void Less_than_the_configured_percentage_of_the_day_remaining_is_degraded()
    {
        var result = BudgetHealthCheck.Evaluate(new BudgetUsage(9_600, 9_600), Allowance, 5);

        Assert.Multiple(
            () => Assert.Equal(HealthStatus.Degraded, result.Status),
            () => Assert.Equal("Daily request budget low (4.0% remaining)", result.Description));
    }

    [Fact]
    public void An_exhausted_monthly_budget_is_unhealthy()
    {
        var result = BudgetHealthCheck.Evaluate(new BudgetUsage(100, 300_000), Allowance, 5);

        Assert.Multiple(
            () => Assert.Equal(HealthStatus.Unhealthy, result.Status),
            () => Assert.Equal("Monthly request budget exhausted (300000/300000)", result.Description));
    }

    [Fact]
    public void An_exhausted_daily_budget_is_unhealthy()
    {
        var result = BudgetHealthCheck.Evaluate(new BudgetUsage(10_000, 10_000), Allowance, 5);

        Assert.Multiple(
            () => Assert.Equal(HealthStatus.Unhealthy, result.Status),
            () => Assert.Equal("Daily request budget exhausted (10000/10000)", result.Description));
    }

    [Fact]
    public void A_raised_budget_limit_changes_the_verdict_without_new_usage()
    {
        var usage = new BudgetUsage(100, 290_000);

        var before = BudgetHealthCheck.Evaluate(usage, Allowance, 5);
        var after = BudgetHealthCheck.Evaluate(usage, Allowance with { Monthly = 600_000 }, 5);

        Assert.Multiple(
            () => Assert.Equal(HealthStatus.Degraded, before.Status),
            () => Assert.Equal(HealthStatus.Healthy, after.Status));
    }

    [Fact]
    public void The_usage_state_publishes_the_latest_values()
    {
        var state = new BudgetUsageState();

        state.Update(3, 30);

        Assert.Equal(new BudgetUsage(3, 30), state.Current);
    }
}
