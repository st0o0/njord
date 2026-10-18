using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Health;
using Njord.Core.Configuration.Polling;

namespace Njord.Budget.Health;

internal sealed class BudgetHealthCheck(
    BudgetUsageState state,
    ConfigView config,
    IOptionsMonitor<HealthOptions> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(Evaluate(state.Current, config.Polling.ToBudgetAllowance(), options.CurrentValue.BudgetDegradedBelowPercent));

    internal static HealthCheckResult Evaluate(BudgetUsage usage, BudgetAllowance allowance, double degradedBelowPercent)
    {
        if (usage.Monthly >= allowance.Monthly)
        {
            return HealthCheckResult.Unhealthy($"Monthly request budget exhausted ({usage.Monthly}/{allowance.Monthly})");
        }

        if (usage.Daily >= allowance.Daily)
        {
            return HealthCheckResult.Unhealthy($"Daily request budget exhausted ({usage.Daily}/{allowance.Daily})");
        }

        var monthlyRemaining = RemainingPercent(usage.Monthly, allowance.Monthly);
        var dailyRemaining = RemainingPercent(usage.Daily, allowance.Daily);

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
