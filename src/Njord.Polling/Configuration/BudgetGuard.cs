using System.Numerics;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Core.Polling;

namespace Njord.Polling.Configuration;

internal static class BudgetGuard
{
    internal const double MonthlyBudgetGuardFactor = 0.8;

    internal const int FloodEnsembleWeight = 5;

    internal const int SeasonalCallsPerMonth = 30;

    private static readonly TimeSpan Month = TimeSpan.FromDays(30);

    internal static string? Violation(
        NjordOptions scope, PollingOptions polling, DomainRequirements requirements, ForecastOptions forecast,
        EnsembleOptions ensemble, FloodOptions? flood = null)
    {
        var locationCount = scope.Locations.Count;

        var weatherRequired = requirements.Requires(DomainKind.Forecast);
        var ensembleRequired = requirements.Requires(DomainKind.Ensemble);
        var otherDomains = new[] { DomainKind.AirQuality, DomainKind.Flood, DomainKind.Marine, DomainKind.Satellite }.Count(requirements.Requires);
        var weatherCallWeight = weatherRequired
            ? CallWeight.Of(BitOperations.PopCount((ulong)requirements.AllForecast), forecast.ForecastDays) * Math.Max(1, forecast.Models.Length)
            : 0;

        var ensembleCallWeight = 0;
        if (ensembleRequired)
        {
            var hasModels = requirements.AllEnsemble.HasFlag(EnsembleParams.Models);
            var spreadVarCount = BitOperations.PopCount((ulong)(requirements.AllEnsemble & ~EnsembleParams.Models));
            var ensembleVars = hasModels
                ? BitOperations.PopCount((ulong)requirements.AllForecast) + spreadVarCount
                : spreadVarCount;
            var ensembleModelCount = hasModels ? Math.Max(1, ensemble.Models.Length) : 1;
            ensembleCallWeight = CallWeight.Of(ensembleVars, ensemble.ForecastDays) * ensembleModelCount;
        }

        var domainCallsPerCycle = locationCount * ((weatherRequired ? 1 : 0) + (ensembleRequired ? 1 : 0) + otherDomains);
        var floodExtraWeight = requirements.Requires(DomainKind.Flood) && flood is { IncludeEnsemble: true } ? FloodEnsembleWeight - 1 : 0;
        var callsPerCycle = locationCount * (weatherCallWeight + ensembleCallWeight + otherDomains + floodExtraWeight);

        var cyclesPerMonth = Month.TotalMinutes / polling.PollInterval.TotalMinutes;
        var seasonalCalls = requirements.Requires(DomainKind.Seasonal) ? locationCount * SeasonalCallsPerMonth : 0;
        var projectedMonthly = (long)Math.Round(callsPerCycle * cyclesPerMonth) + seasonalCalls;

        var limit = polling.Budget.RequestsPerMonth;
        var usagePercent = limit > 0 ? (double)projectedMonthly / limit * 100 : 0;
        if (usagePercent <= MonthlyBudgetGuardFactor * 100)
        {
            return null;
        }

        var guard = (int)Math.Round(limit * MonthlyBudgetGuardFactor);
        return
            $"Projected API usage of {projectedMonthly} effective requests/month ({domainCallsPerCycle} domain calls per cycle " +
            $"every {polling.PollInterval.TotalMinutes:0} min, weight {weatherCallWeight + ensembleCallWeight}) exceeds the " +
            $"guard of {guard} (80% of the {limit}/month budget). " +
            "Reduce locations or increase PollInterval.";
    }
}
