namespace Njord.Core.Configuration.Polling;

public static class BudgetCalculator
{
    public const double MonthlyBudgetGuardFactor = 0.8;

    // Number of domain endpoint calls per location per poll cycle.
    // Currently only Weather (Ensemble) is active. Will increase as more domains are enabled.
    public const int DomainCallsPerLocation = 1;

    private static readonly TimeSpan Month = TimeSpan.FromDays(30);

    public static RequestBudget GetEffectiveBudget(PollingOptions polling) =>
        polling.BudgetOverride ?? RequestBudget.OpenMeteoFreeTier;

    public static BudgetValidation Validate(NjordOptions scope, PollingOptions polling)
    {
        var locationCount = scope.Locations.Count;
        var domainCallsPerCycle = locationCount * DomainCallsPerLocation;

        var resolved = scope.Parameters.Resolve();

        var cyclesPerMonth = Month.TotalMinutes / polling.PollInterval.TotalMinutes;
        var projectedMonthly = (long)Math.Round(domainCallsPerCycle * cyclesPerMonth * resolved.ApiCallWeight);

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
            DomainCallsPerCycle: domainCallsPerCycle,
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
            $"Projected API usage of {projection.ProjectedMonthlyCalls} effective requests/month ({projection.DomainCallsPerCycle} domain calls per cycle " +
            $"every {pollInterval.TotalMinutes:0} min, weight {projection.ApiCallWeight}) exceeds the " +
            $"guard of {guard} (80% of the {projection.MonthlyLimit}/month budget). " +
            "Reduce locations, increase PollInterval, or exclude parameters.";
    }
}

public sealed record BudgetValidation(
    long ProjectedMonthlyCalls,
    long MonthlyLimit,
    double UsagePercent,
    bool WithinBudget,
    IReadOnlyList<string> Warnings,
    int DomainCallsPerCycle,
    int ApiCallWeight);
