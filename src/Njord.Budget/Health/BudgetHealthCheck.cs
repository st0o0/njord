using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Configuration.Health;
using Njord.Messages.Budget;

namespace Njord.Budget.Health;

internal sealed class BudgetHealthCheck(
    AtomicStateHolder<BudgetStatusResult> holder,
    IOptions<HealthOptions> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var status = holder.Current;
        return Task.FromResult(status is null
            ? HealthCheckResult.Degraded("No budget data yet")
            : Evaluate(status, options.Value.BudgetDegradedBelowPercent));
    }

    internal static HealthCheckResult Evaluate(BudgetStatusResult status, double degradedBelowPercent)
    {
        if (status.MonthlyUsed >= status.MonthlyLimit)
        {
            return HealthCheckResult.Unhealthy($"Monthly request budget exhausted ({status.MonthlyUsed}/{status.MonthlyLimit})");
        }

        if (status.DailyUsed >= status.DailyLimit)
        {
            return HealthCheckResult.Unhealthy($"Daily request budget exhausted ({status.DailyUsed}/{status.DailyLimit})");
        }

        var monthlyRemaining = RemainingPercent(status.MonthlyUsed, status.MonthlyLimit);
        var dailyRemaining = RemainingPercent(status.DailyUsed, status.DailyLimit);

        if (monthlyRemaining < degradedBelowPercent)
        {
            return HealthCheckResult.Degraded(FormattableString.Invariant($"Monthly request budget low ({monthlyRemaining:F1}% remaining)"));
        }

        return dailyRemaining < degradedBelowPercent
            ? HealthCheckResult.Degraded(FormattableString.Invariant($"Daily request budget low ({dailyRemaining:F1}% remaining)"))
            : HealthCheckResult.Healthy(FormattableString.Invariant($"Budget {monthlyRemaining:F1}% monthly, {dailyRemaining:F1}% daily remaining"));
    }

    private static double RemainingPercent(long used, long limit) =>
        limit <= 0 ? 0 : (limit - used) * 100.0 / limit;
}
