using System.Numerics;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Core.Polling;

namespace Njord.Polling.Configuration;

internal static class BudgetGuard
{
    internal const double MonthlyBudgetGuardFactor = 0.8;

    internal const int FloodEnsembleWeight = 5;

    internal const int SeasonalCallsPerMonth = 30;

    internal const double SeasonalMinimumRequestWeight = 1.3;

    private static readonly TimeSpan _month = TimeSpan.FromDays(30);

    internal static string? Violation(
        NjordOptions scope, PollingOptions polling, DomainRequirements requirements, DomainCallConfig callConfig)
    {
        var locationCount = scope.ResolvedLocations.Count;

        double projectedMonthly = 0;

        if (requirements.Requires(DomainKind.Forecast))
        {
            var weight = ForecastWeight(requirements, callConfig);
            var cycles = _month.TotalMinutes / polling.ResolvedInterval(DomainKind.Forecast).TotalMinutes;
            projectedMonthly += locationCount * weight * cycles;
        }

        if (requirements.Requires(DomainKind.Ensemble))
        {
            var weight = EnsembleWeight(requirements, callConfig);
            var cycles = _month.TotalMinutes / polling.ResolvedInterval(DomainKind.Ensemble).TotalMinutes;
            projectedMonthly += locationCount * weight * cycles;
        }

        if (requirements.Requires(DomainKind.AirQuality))
        {
            var weight = AirQualityWeight(requirements, callConfig);
            var cycles = _month.TotalMinutes / polling.ResolvedInterval(DomainKind.AirQuality).TotalMinutes;
            projectedMonthly += locationCount * weight * cycles;
        }

        if (requirements.Requires(DomainKind.Flood))
        {
            var weight = FloodWeight(requirements, callConfig);
            var cycles = _month.TotalMinutes / polling.ResolvedInterval(DomainKind.Flood).TotalMinutes;
            projectedMonthly += locationCount * weight * cycles;
        }

        if (requirements.Requires(DomainKind.Marine))
        {
            var weight = MarineWeight(requirements, callConfig);
            var cycles = _month.TotalMinutes / polling.ResolvedInterval(DomainKind.Marine).TotalMinutes;
            projectedMonthly += locationCount * weight * cycles;
        }

        projectedMonthly += locationCount * SeasonalWeightPerMonth(requirements, callConfig);

        var total = (long)Math.Round(projectedMonthly);
        var limit = polling.Budget.RequestsPerMonth;
        var usagePercent = limit > 0 ? (double)total / limit * 100 : 0;
        if (usagePercent <= MonthlyBudgetGuardFactor * 100)
        {
            return null;
        }

        var guard = (int)Math.Round(limit * MonthlyBudgetGuardFactor);
        return
            $"Projected API usage of {total} effective requests/month exceeds the " +
            $"guard of {guard} (80% of the {limit}/month budget). " +
            "Reduce locations, increase domain intervals, or increase PollInterval.";
    }

    private static double ForecastWeight(DomainRequirements requirements, DomainCallConfig config) =>
        CallWeight.Of(BitOperations.PopCount((ulong)requirements.AllForecast), config.ForecastDays)
            * Math.Max(1, config.ForecastModelCount);

    private static double EnsembleWeight(DomainRequirements requirements, DomainCallConfig config)
    {
        var spreadVarCount = BitOperations.PopCount((ulong)requirements.AllEnsemble);
        var modelCount = Math.Max(1, config.EnsembleModelCount);
        var ensembleVars = modelCount > 1
            ? BitOperations.PopCount((ulong)requirements.AllForecast) + spreadVarCount
            : spreadVarCount;
        return CallWeight.Of(ensembleVars, config.EnsembleDays) * modelCount;
    }

    private static double AirQualityWeight(DomainRequirements requirements, DomainCallConfig config) =>
        CallWeight.Of(BitOperations.PopCount((ulong)requirements.AllAirQuality), config.AirQualityDays);

    private static double FloodWeight(DomainRequirements requirements, DomainCallConfig config) =>
        CallWeight.Of(BitOperations.PopCount((ulong)requirements.AllFlood), config.FloodDays)
            + (config.FloodIncludeEnsemble ? FloodEnsembleWeight - 1 : 0);

    private static double MarineWeight(DomainRequirements requirements, DomainCallConfig config) =>
        CallWeight.Of(BitOperations.PopCount((ulong)requirements.AllMarine), config.MarineDays);

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
