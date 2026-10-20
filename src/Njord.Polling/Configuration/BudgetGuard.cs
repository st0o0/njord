using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Core.Polling;
using Njord.Forecast;

namespace Njord.Polling.Configuration;

internal static class BudgetGuard
{
    internal const double MonthlyBudgetGuardFactor = 0.8;

    internal const int FloodEnsembleWeight = 5;

    internal const int SeasonalCallsPerMonth = 30;

    private static readonly TimeSpan Month = TimeSpan.FromDays(30);

    internal static string? Violation(
        NjordOptions scope, WeatherOptions weather, PollingOptions polling, ResolvedRequirements resolved,
        FloodOptions? flood = null, int weatherModelCount = 1)
    {
        var requirements = resolved.Value;
        var locationCount = scope.Locations.Count;

        var weatherRequired = requirements.Requires(DomainKind.Weather);
        var otherDomains = new[] { DomainKind.AirQuality, DomainKind.Flood, DomainKind.Marine, DomainKind.Satellite }.Count(requirements.Requires);
        var apiCallWeight = weatherRequired
            ? CallWeight.Of(System.Numerics.BitOperations.PopCount((ulong)requirements.AllWeather), weather.ForecastDays) * Math.Max(1, weatherModelCount)
            : 0;

        var domainCallsPerCycle = locationCount * ((weatherRequired ? 1 : 0) + otherDomains);
        var floodExtraWeight = requirements.Requires(DomainKind.Flood) && flood is { IncludeEnsemble: true } ? FloodEnsembleWeight - 1 : 0;
        var callsPerCycle = locationCount * (apiCallWeight + otherDomains + floodExtraWeight);

        var cyclesPerMonth = Month.TotalMinutes / polling.PollInterval.TotalMinutes;
        var seasonalCalls = requirements.Requires(DomainKind.Seasonal) ? locationCount * SeasonalCallsPerMonth : 0;
        var projectedMonthly = (long)Math.Round(callsPerCycle * cyclesPerMonth) + seasonalCalls;

        var limit = polling.EffectiveBudget.RequestsPerMonth;
        var usagePercent = limit > 0 ? (double)projectedMonthly / limit * 100 : 0;
        if (usagePercent <= MonthlyBudgetGuardFactor * 100)
        {
            return null;
        }

        var guard = (int)Math.Round(limit * MonthlyBudgetGuardFactor);
        return
            $"Projected API usage of {projectedMonthly} effective requests/month ({domainCallsPerCycle} domain calls per cycle " +
            $"every {polling.PollInterval.TotalMinutes:0} min, weight {apiCallWeight}) exceeds the " +
            $"guard of {guard} (80% of the {limit}/month budget). " +
            "Reduce locations or increase PollInterval.";
    }
}
