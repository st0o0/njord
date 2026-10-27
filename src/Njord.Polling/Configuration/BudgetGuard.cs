using System.Numerics;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Core.Polling;
using Njord.Forecast;
using Njord.Forecast.Forecast;

namespace Njord.Polling.Configuration;

internal static class BudgetGuard
{
    internal const double MonthlyBudgetGuardFactor = 0.8;

    internal const int FloodEnsembleWeight = 5;

    internal const int SeasonalCallsPerMonth = 30;

    internal const int EnsembleSpreadVariableCount = 3;

    private static readonly TimeSpan Month = TimeSpan.FromDays(30);

    internal static string? Violation(
        NjordOptions scope, PollingOptions polling, FeatureRequirementsOptions featureRequirements, ForecastOptions weather,
        FloodOptions? flood = null)
    {
        var requirements = featureRequirements.Requirements;
        var locationCount = scope.Locations.Count;

        var weatherRequired = requirements.Requires(DomainKind.Forecast);
        var ensembleRequired = requirements.Requires(DomainKind.Ensemble);
        var otherDomains = new[] { DomainKind.AirQuality, DomainKind.Flood, DomainKind.Marine, DomainKind.Satellite }.Count(requirements.Requires);
        var weatherCallWeight = weatherRequired
            ? CallWeight.Of(BitOperations.PopCount((ulong)requirements.AllForecast), weather.ForecastDays)
            : 0;

        var ensembleCallWeight = 0;
        if (ensembleRequired)
        {
            var ensembleModels = featureRequirements.EnsembleModels;
            var hasModels = requirements.AllEnsemble.HasFlag(EnsembleParams.Models);
            var ensembleVars = hasModels
                ? BitOperations.PopCount((ulong)requirements.AllForecast) + EnsembleSpreadVariableCount
                : EnsembleSpreadVariableCount;
            var ensembleModelCount = hasModels ? ensembleModels.Length : 1;
            ensembleCallWeight = CallWeight.Of(ensembleVars, weather.ForecastDays) * ensembleModelCount;
        }

        var domainCallsPerCycle = locationCount * ((weatherRequired ? 1 : 0) + (ensembleRequired ? 1 : 0) + otherDomains);
        var floodExtraWeight = requirements.Requires(DomainKind.Flood) && flood is { IncludeEnsemble: true } ? FloodEnsembleWeight - 1 : 0;
        var callsPerCycle = locationCount * (weatherCallWeight + ensembleCallWeight + otherDomains + floodExtraWeight);

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
            $"every {polling.PollInterval.TotalMinutes:0} min, weight {weatherCallWeight + ensembleCallWeight}) exceeds the " +
            $"guard of {guard} (80% of the {limit}/month budget). " +
            "Reduce locations or increase PollInterval.";
    }
}
