using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Njord.Core.Actors;
using Njord.Core.Configuration.Health;
using Njord.Messages.Budget;

namespace Njord.Budget.Health;

internal sealed class BudgetHealthCheck(
    ActorRegistry actorRegistry,
    IOptions<HealthOptions> options) : IHealthCheck
{
    private static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(3);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!actorRegistry.TryGet<IBudgetTracker>(out var budgetTracker))
        {
            return HealthCheckResult.Unhealthy("Budget tracker not available");
        }

        try
        {
            var result = await budgetTracker.Ask<BudgetStatusResult>(
                new QueryBudgetStatus(), AskTimeout, cancellationToken);
            return Evaluate(result, options.Value.BudgetDegradedBelowPercent);
        }
        catch (AskTimeoutException)
        {
            return HealthCheckResult.Unhealthy("Budget tracker unresponsive");
        }
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
