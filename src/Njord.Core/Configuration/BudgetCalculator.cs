using Njord.Weather;

namespace Njord.Core.Configuration;

public static class BudgetCalculator
{
    private static readonly TimeSpan Month = TimeSpan.FromDays(30);

    public static RequestBudget GetEffectiveBudget(NjordOptions options) =>
        options.BudgetOverride ?? RequestBudget.OpenMeteoFreeTier;

    public static BudgetValidation Validate(NjordOptions options)
    {
        var totalModelsPerCycle = options.Locations
            .Sum(loc => options.Models.Union(loc.Models ?? [], StringComparer.OrdinalIgnoreCase).Count());

        var resolved = ParameterRegistry.Resolve(
            options.Parameters.Groups,
            options.Parameters.Extra,
            options.Parameters.Exclude);

        var cyclesPerMonth = Month.TotalMinutes / options.PollInterval.TotalMinutes;
        var projectedMonthly = (long)Math.Round(totalModelsPerCycle * cyclesPerMonth * resolved.ApiCallWeight);

        var budget = GetEffectiveBudget(options);
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
}

public sealed record BudgetValidation(
    long ProjectedMonthlyCalls,
    long MonthlyLimit,
    double UsagePercent,
    bool WithinBudget,
    IReadOnlyList<string> Warnings,
    int TotalModelsPerCycle,
    int ApiCallWeight);
