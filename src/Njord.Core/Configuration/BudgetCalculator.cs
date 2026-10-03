using Njord.Domain.Weather;

namespace Njord.Configuration;

public static class BudgetCalculator
{
    public static RequestBudget GetEffectiveBudget(NjordOptions options) =>
        options.BudgetOverride ?? RequestBudget.OpenMeteoFreeTier;

    public static BudgetValidation Validate(NjordOptions options)
    {
        var totalModelsPerCycle = options.Locations
            .Sum(loc => options.Models.Union(loc.Models ?? [], StringComparer.OrdinalIgnoreCase).Count());

        var hourlyCount = CountHourlyParameters(options.Parameters);
        var apiCallWeight = (int)Math.Ceiling(hourlyCount / 10.0);

        var callsPerCycle = totalModelsPerCycle * apiCallWeight;
        var cyclesPerDay = (int)(TimeSpan.FromDays(1) / options.PollInterval);
        var projectedMonthly = (long)callsPerCycle * cyclesPerDay * 30;

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
            Warnings: warnings);
    }

    public static int CountHourlyParameters(ParameterOptions paramOptions)
    {
        var resolved = ParameterRegistry.Resolve(paramOptions.Groups, paramOptions.Extra, paramOptions.Exclude);
        return Math.Max(1, resolved.Hourly.Count);
    }
}

public sealed record BudgetValidation(
    long ProjectedMonthlyCalls,
    long MonthlyLimit,
    double UsagePercent,
    bool WithinBudget,
    IReadOnlyList<string> Warnings);
