using Microsoft.Extensions.Diagnostics.HealthChecks;
using Njord.Budget.Health;
using Njord.Messages.Budget;

namespace Njord.Budget.Tests;

public sealed class BudgetHealthCheckSpec
{
    [Fact]
    public void Plenty_of_budget_remaining_is_healthy()
    {
        var result = BudgetHealthCheck.Evaluate(new BudgetStatusResult(1_000, 100, 300_000, 10_000), 5);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public void Less_than_the_configured_percentage_of_the_month_remaining_is_degraded()
    {
        var result = BudgetHealthCheck.Evaluate(new BudgetStatusResult(288_000, 100, 300_000, 10_000), 5);

        Assert.Multiple(
            () => Assert.Equal(HealthStatus.Degraded, result.Status),
            () => Assert.Equal("Monthly request budget low (4.0% remaining)", result.Description));
    }

    [Fact]
    public void Less_than_the_configured_percentage_of_the_day_remaining_is_degraded()
    {
        var result = BudgetHealthCheck.Evaluate(new BudgetStatusResult(9_600, 9_600, 300_000, 10_000), 5);

        Assert.Multiple(
            () => Assert.Equal(HealthStatus.Degraded, result.Status),
            () => Assert.Equal("Daily request budget low (4.0% remaining)", result.Description));
    }

    [Fact]
    public void An_exhausted_monthly_budget_is_unhealthy()
    {
        var result = BudgetHealthCheck.Evaluate(new BudgetStatusResult(300_000, 100, 300_000, 10_000), 5);

        Assert.Multiple(
            () => Assert.Equal(HealthStatus.Unhealthy, result.Status),
            () => Assert.Equal("Monthly request budget exhausted (300000/300000)", result.Description));
    }

    [Fact]
    public void An_exhausted_daily_budget_is_unhealthy()
    {
        var result = BudgetHealthCheck.Evaluate(new BudgetStatusResult(10_000, 10_000, 300_000, 10_000), 5);

        Assert.Multiple(
            () => Assert.Equal(HealthStatus.Unhealthy, result.Status),
            () => Assert.Equal("Daily request budget exhausted (10000/10000)", result.Description));
    }

    [Fact]
    public void A_raised_budget_limit_changes_the_verdict_without_new_usage()
    {
        var status = new BudgetStatusResult(290_000, 100, 300_000, 10_000);

        var before = BudgetHealthCheck.Evaluate(status, 5);
        var after = BudgetHealthCheck.Evaluate(status with { MonthlyLimit = 600_000 }, 5);

        Assert.Multiple(
            () => Assert.Equal(HealthStatus.Degraded, before.Status),
            () => Assert.Equal(HealthStatus.Healthy, after.Status));
    }

    [Fact(Timeout = 5000)]
    public async Task The_check_reports_degraded_when_the_tracker_is_not_registered()
    {
        var check = new BudgetHealthCheck(
            new Akka.Hosting.ActorRegistry(),
            Microsoft.Extensions.Options.Options.Create(new Njord.Core.Configuration.Health.HealthOptions()));

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Multiple(
            () => Assert.Equal(HealthStatus.Degraded, result.Status),
            () => Assert.Equal("Budget tracker not available", result.Description));
    }
}
