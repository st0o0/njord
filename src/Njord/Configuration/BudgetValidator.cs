namespace Njord.Configuration;

public static class BudgetValidator
{
    public static BudgetValidation Validate(NjordOptions options)
    {
        var totalModelsPerCycle = options.Locations
            .Sum(loc => loc.ResolveModels(options.Models).Count);

        var hourlyCount = CountHourlyParameters(options.Parameters);
        var apiCallWeight = (int)Math.Ceiling(hourlyCount / 10.0);

        var callsPerCycle = totalModelsPerCycle * apiCallWeight;
        var cyclesPerDay = (int)(TimeSpan.FromDays(1) / options.PollInterval);
        var projectedMonthly = (long)callsPerCycle * cyclesPerDay * 30;

        var budget = options.EffectiveBudget;
        var usagePercent = budget.RequestsPerMonth > 0
            ? (double)projectedMonthly / budget.RequestsPerMonth * 100
            : 0;

        var warnings = new List<string>();
        if (usagePercent > 80 && usagePercent <= 100)
            warnings.Add($"Projected API usage is {usagePercent:F0}% of monthly budget");

        return new BudgetValidation(
            ProjectedMonthlyCalls: projectedMonthly,
            MonthlyLimit: budget.RequestsPerMonth,
            UsagePercent: usagePercent,
            WithinBudget: usagePercent <= 100,
            Warnings: warnings);
    }

    private static int CountHourlyParameters(ParameterOptions paramOptions)
    {
        var count = 0;
        foreach (var group in paramOptions.Groups)
        {
            count += group.ToLowerInvariant() switch
            {
                "weather" => 31,
                "solar" => 9,
                "soil" => 11,
                _ => 0,
            };
        }
        count += paramOptions.Extra.Count;
        count -= paramOptions.Exclude.Count;
        return Math.Max(1, count);
    }
}

public sealed record BudgetValidation(
    long ProjectedMonthlyCalls,
    long MonthlyLimit,
    double UsagePercent,
    bool WithinBudget,
    IReadOnlyList<string> Warnings);
