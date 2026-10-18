using Njord.Weather;

namespace Njord.Core.Configuration.Polling;

public static class BudgetCalculator
{
    public const double MonthlyBudgetGuardFactor = 0.8;

    private static readonly TimeSpan Month = TimeSpan.FromDays(30);

    public static RequestBudget GetEffectiveBudget(PollingOptions polling) =>
        polling.BudgetOverride ?? RequestBudget.OpenMeteoFreeTier;

    public static BudgetValidation Validate(NjordOptions scope, PollingOptions polling)
    {
        var totalModelsPerCycle = scope.Locations
            .Sum(loc => scope.Models.Union(loc.Models ?? [], StringComparer.OrdinalIgnoreCase).Count());

        var resolved = scope.Parameters.Resolve();

        var cyclesPerMonth = Month.TotalMinutes / polling.PollInterval.TotalMinutes;
        var projectedMonthly = (long)Math.Round(totalModelsPerCycle * cyclesPerMonth * resolved.ApiCallWeight);

        var budget = GetEffectiveBudget(polling);
        var usagePercent = budget.RequestsPerMonth > 0
            ? (double)projectedMonthly / budget.RequestsPerMonth * 100
            : 0;

        var warnings = new List<string>();
        if (usagePercent is > 80 and <= 100)
        {
            warnings.Add($"Projected API usage is {usagePercent:F0}% of monthly budget");
        }

        return new BudgetValidation(
            ProjectedMonthlyCalls: projectedMonthly,
            MonthlyLimit: budget.RequestsPerMonth,
            UsagePercent: usagePercent,
            WithinBudget: usagePercent <= 100,
            Warnings: warnings,
            TotalModelsPerCycle: totalModelsPerCycle,
            ApiCallWeight: resolved.ApiCallWeight);
    }

    public static string? GuardViolation(BudgetValidation projection, TimeSpan pollInterval)
    {
        if (projection.UsagePercent <= MonthlyBudgetGuardFactor * 100)
        {
            return null;
        }

        var guard = (int)Math.Round(projection.MonthlyLimit * MonthlyBudgetGuardFactor);
        return
            $"Projected API usage of {projection.ProjectedMonthlyCalls} effective requests/month ({projection.TotalModelsPerCycle} model-location pairs " +
            $"every {pollInterval.TotalMinutes:0} min, weight {projection.ApiCallWeight}) exceeds the " +
            $"guard of {guard} (80% of the {projection.MonthlyLimit}/month budget). " +
            "Reduce locations/models, increase PollInterval, or exclude parameters.";
    }
}

public sealed record BudgetValidation(
    long ProjectedMonthlyCalls,
    long MonthlyLimit,
    double UsagePercent,
    bool WithinBudget,
    IReadOnlyList<string> Warnings,
    int TotalModelsPerCycle,
    int ApiCallWeight);
