using Njord.Core.Domain;
using Njord.Forecast;

namespace Njord.Core.Configuration.Polling;

public static class BudgetCalculator
{
    public const double MonthlyBudgetGuardFactor = 0.8;

    private static readonly TimeSpan Month = TimeSpan.FromDays(30);

    public static RequestBudget GetEffectiveBudget(PollingOptions polling) =>
        polling.BudgetOverride ?? RequestBudget.OpenMeteoFreeTier;

    public static int ComputeWeight(int hourlyVariableCount, int forecastDays) =>
        (int)Math.Ceiling(hourlyVariableCount / 10.0) * (int)Math.Ceiling(forecastDays / 14.0);

    public static BudgetValidation Validate(NjordOptions scope, WeatherOptions weather, PollingOptions polling, ResolvedRequirements resolved)
    {
        var requirements = resolved.Value;
        var locationCount = scope.Locations.Count;

        var weatherRequired = requirements.Requires(DomainKind.Weather);
        var otherDomains = new[] { DomainKind.AirQuality, DomainKind.Flood, DomainKind.Marine }.Count(requirements.Requires);
        var apiCallWeight = weatherRequired ? ComputeWeight(requirements.AllWeather.FlagCount(), weather.ForecastDays) : 0;

        var domainCallsPerCycle = locationCount * ((weatherRequired ? 1 : 0) + otherDomains);
        var callsPerCycle = locationCount * (apiCallWeight + otherDomains);

        var cyclesPerMonth = Month.TotalMinutes / polling.PollInterval.TotalMinutes;
        var projectedMonthly = (long)Math.Round(callsPerCycle * cyclesPerMonth);

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
            ApiCallWeight: apiCallWeight);
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
            "Reduce locations or increase PollInterval.";
    }
}
