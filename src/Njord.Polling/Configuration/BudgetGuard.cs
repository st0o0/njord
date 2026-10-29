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

    internal const double SeasonalMinimumRequestWeight = 1.3;

    private const double SatelliteWindowHours = 14 * 24;

    private static readonly TimeSpan Month = TimeSpan.FromDays(30);

    private static readonly DomainKind[] PerCycleDomains =
    [
        DomainKind.Forecast, DomainKind.Ensemble, DomainKind.AirQuality, DomainKind.Flood, DomainKind.Marine, DomainKind.Satellite,
    ];

    internal static string? Violation(
        NjordOptions scope, PollingOptions polling, DomainRequirements requirements, DomainCallConfig callConfig)
    {
        var locationCount = scope.Locations.Count;

        var domainCallsPerCycle = locationCount * PerCycleDomains.Count(requirements.Requires);
        var weightPerCycle = ForecastWeight(requirements, callConfig)
            + EnsembleWeight(requirements, callConfig)
            + AirQualityWeight(requirements, callConfig)
            + FloodWeight(requirements, callConfig)
            + MarineWeight(requirements, callConfig)
            + SatelliteWeight(requirements, callConfig);

        var cyclesPerMonth = Month.TotalMinutes / polling.PollInterval.TotalMinutes;
        var seasonalCalls = locationCount * SeasonalWeightPerMonth(requirements, callConfig);
        var projectedMonthly = (long)Math.Round(locationCount * weightPerCycle * cyclesPerMonth + seasonalCalls);

        var limit = polling.Budget.RequestsPerMonth;
        var usagePercent = limit > 0 ? (double)projectedMonthly / limit * 100 : 0;
        if (usagePercent <= MonthlyBudgetGuardFactor * 100)
        {
            return null;
        }

        var guard = (int)Math.Round(limit * MonthlyBudgetGuardFactor);
        return
            $"Projected API usage of {projectedMonthly} effective requests/month ({domainCallsPerCycle} domain calls per cycle " +
            $"every {polling.PollInterval.TotalMinutes:0} min, weight {weightPerCycle:0.##}) exceeds the " +
            $"guard of {guard} (80% of the {limit}/month budget). " +
            "Reduce locations or increase PollInterval.";
    }

    private static double ForecastWeight(DomainRequirements requirements, DomainCallConfig config) =>
        requirements.Requires(DomainKind.Forecast)
            ? CallWeight.Of(BitOperations.PopCount((ulong)requirements.AllForecast), config.ForecastDays)
                * Math.Max(1, config.ForecastModelCount)
            : 0;

    private static double EnsembleWeight(DomainRequirements requirements, DomainCallConfig config)
    {
        if (!requirements.Requires(DomainKind.Ensemble))
        {
            return 0;
        }

        var hasModels = requirements.AllEnsemble.HasFlag(EnsembleParams.Models);
        var spreadVarCount = BitOperations.PopCount((ulong)(requirements.AllEnsemble & ~EnsembleParams.Models));
        var ensembleVars = hasModels
            ? BitOperations.PopCount((ulong)requirements.AllForecast) + spreadVarCount
            : spreadVarCount;
        var modelCount = hasModels ? Math.Max(1, config.EnsembleModelCount) : 1;
        return CallWeight.Of(ensembleVars, config.EnsembleDays) * modelCount;
    }

    private static double AirQualityWeight(DomainRequirements requirements, DomainCallConfig config) =>
        requirements.Requires(DomainKind.AirQuality)
            ? CallWeight.Of(BitOperations.PopCount((ulong)requirements.AllAirQuality), config.AirQualityDays)
            : 0;

    private static double FloodWeight(DomainRequirements requirements, DomainCallConfig config) =>
        requirements.Requires(DomainKind.Flood)
            ? CallWeight.Of(BitOperations.PopCount((ulong)requirements.AllFlood), config.FloodDays)
                + (config.FloodIncludeEnsemble ? FloodEnsembleWeight - 1 : 0)
            : 0;

    private static double MarineWeight(DomainRequirements requirements, DomainCallConfig config) =>
        requirements.Requires(DomainKind.Marine)
            ? CallWeight.Of(BitOperations.PopCount((ulong)requirements.AllMarine), config.MarineDays)
            : 0;

    private static double SatelliteWeight(DomainRequirements requirements, DomainCallConfig config) =>
        requirements.Requires(DomainKind.Satellite)
            ? Math.Ceiling(BitOperations.PopCount((ulong)requirements.AllSatellite) / 10.0)
                * (config.SatellitePastHours / SatelliteWindowHours)
            : 0;

    private static double SeasonalWeightPerMonth(DomainRequirements requirements, DomainCallConfig config)
    {
        if (!requirements.Requires(DomainKind.Seasonal))
        {
            return 0;
        }

        var perRequest = Math.Max(
            CallWeight.Of(BitOperations.PopCount((ulong)requirements.AllSeasonal), config.SeasonalMonths * 30),
            SeasonalMinimumRequestWeight);
        return SeasonalCallsPerMonth * perRequest;
    }
}
