using Njord.Core.Configuration.Alerts;
using Njord.Core.Configuration.Consensus;
using Njord.Core.Configuration.Derived;
using Njord.Core.Configuration.Indices;
using Njord.Core.Configuration.Polling;
using Njord.Core.Configuration.Trends;
using Njord.Messages.Config;
using Njord.Weather;

namespace Njord.Core.Configuration;

// The one mapping point from options objects to the configuration contracts in Njord.Messages.
public static class ConfigSnapshotMapping
{
    public static ConfigSnapshot ToSnapshot(
        NjordOptions scope,
        PollingOptions polling,
        ConsensusOptions consensus,
        AlertOptions alerts,
        DerivedOptions derived,
        TrendOptions trends,
        IndexOptions indices) =>
        new(
            ToScope(scope),
            ToPolling(polling),
            ToProjection(BudgetCalculator.Validate(scope, polling)),
            new AnalysisSections(
                new ConsensusSection(consensus.Enabled, consensus.Method, consensus.TrimPercent, consensus.MinQuorum),
                ToAlerts(alerts),
                new DerivedSection(derived.Enabled),
                new TrendSection(trends.Enabled),
                ToIndices(indices)));

    public static ConfigSnapshot ToSnapshot(this ConfigView view) =>
        ToSnapshot(view.Scope, view.Polling, view.Consensus, view.Alerts, view.Derived, view.Trends, view.Indices);

    // Locations in configuration order; models deduplicated in first-seen order with their coverage.
    public static CatalogResult ToCatalog(this NjordOptions scope)
    {
        var locations = new List<CatalogLocation>();
        var models = new List<CatalogModel>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var location in scope.Locations)
        {
            var resolved = ResolveModels(scope, location);
            locations.Add(new CatalogLocation(new LocationId(location.Name), location.Latitude, location.Longitude, resolved));

            foreach (var modelId in resolved.Where(seen.Add))
            {
                models.Add(new CatalogModel(modelId, ModelCoverageRegistry.Get(modelId)));
            }
        }

        return new CatalogResult(locations, models);
    }

    private static ForecastScope ToScope(NjordOptions scope) =>
        new(
            [
                .. scope.Locations.Select(l => new LocationEntry(
                    new LocationId(l.Name), l.Latitude, l.Longitude, [.. l.Models ?? []], ResolveModels(scope, l)))
            ],
            [.. scope.Models],
            [.. scope.Horizons],
            scope.ForecastDays,
            new ParameterSelection([.. scope.Parameters.Groups], [.. scope.Parameters.Extra], [.. scope.Parameters.Exclude]));

    private static List<string> ResolveModels(NjordOptions scope, LocationOptions location) =>
        [.. scope.Models.Union(location.Models ?? [], StringComparer.OrdinalIgnoreCase)];

    private static PollingSection ToPolling(PollingOptions polling)
    {
        var effective = BudgetCalculator.GetEffectiveBudget(polling);
        return new PollingSection(
            polling.PollInterval,
            polling.DiscoveryInterval,
            polling.BudgetOverride is { } o ? new BudgetLimits(o.RequestsPerMonth, o.RequestsPerMinute) : null,
            new BudgetLimits(effective.RequestsPerMonth, effective.RequestsPerMinute),
            RequestBudget.OpenMeteoFreeTierDailyLimit);
    }

    private static BudgetProjection ToProjection(BudgetValidation validation) =>
        new(validation.ProjectedMonthlyCalls, validation.MonthlyLimit, validation.UsagePercent,
            validation.WithinBudget, [.. validation.Warnings]);

    private static AlertSection ToAlerts(AlertOptions a) =>
        new(
            a.Enabled,
            [.. a.FrostThresholds],
            [.. a.HeatThresholds],
            [.. a.StormGustThresholds],
            a.HeavyRainHourlyThreshold,
            a.HeavyRainDailyThreshold,
            a.PressureDropThreshold,
            a.PressureDropSevereThreshold,
            a.FogPersistentHours,
            a.CapeThreshold,
            a.ThunderstormPrecipThreshold,
            a.ThunderstormGustThreshold,
            a.IceThreshold,
            [.. a.WindChillThresholds],
            [.. a.VisibilityThresholds],
            [.. a.TropicalNightThresholds],
            [.. a.HumidityThresholds]);

    private static IndexSection ToIndices(IndexOptions indices) =>
        new(
            indices.Enabled,
            ToValues(indices.Preferences),
            ToValues(indices.ScoreOverrides),
            [
                .. indices.LocationOverrides.Select(o => new LocationIndexPreferences(
                    o.Location, ToValues(o.Preferences), ToValues(o.ScoreOverrides)))
            ]);

    private static Dictionary<string, IndexPreferenceValues> ToValues(IDictionary<string, IndexPreferences> overrides) =>
        overrides.ToDictionary(p => p.Key, p => ToValues(p.Value), StringComparer.OrdinalIgnoreCase);

    private static IndexPreferenceValues ToValues(IndexPreferences p) =>
        new(
            p.IdealOutdoorTemp,
            p.RunningIdealTempLow,
            p.RunningIdealTempHigh,
            p.BbqMinTemp,
            p.BbqIdealWindLow,
            p.BbqIdealWindHigh,
            p.IndoorTemp,
            p.HeatSensitivity,
            p.HumiditySensitivity,
            p.WindSensitivity,
            p.RainSensitivity);
}
