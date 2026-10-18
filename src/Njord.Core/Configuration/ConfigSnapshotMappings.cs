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
public static class ConfigSnapshotMappings
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
            scope.ToScope(),
            polling.ToSection(),
            BudgetCalculator.Validate(scope, polling).ToProjection(),
            new AnalysisSections(
                consensus.ToSection(),
                alerts.ToSection(),
                derived.ToSection(),
                trends.ToSection(),
                indices.ToSection()));

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

    public static ConsensusSection ToSection(this ConsensusOptions consensus) =>
        new(consensus.Enabled, consensus.Method, consensus.TrimPercent, consensus.MinQuorum);

    public static DerivedSection ToSection(this DerivedOptions derived) => new(derived.Enabled);

    public static TrendSection ToSection(this TrendOptions trends) => new(trends.Enabled);

    public static ForecastScope ToScope(this NjordOptions scope) =>
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

    public static PollingSection ToSection(this PollingOptions polling)
    {
        var effective = BudgetCalculator.GetEffectiveBudget(polling);
        return new PollingSection(
            polling.PollInterval,
            polling.DiscoveryInterval,
            polling.BudgetOverride is { } o ? new BudgetLimits(o.RequestsPerMonth, o.RequestsPerMinute) : null,
            new BudgetLimits(effective.RequestsPerMonth, effective.RequestsPerMinute),
            RequestBudget.OpenMeteoFreeTierDailyLimit);
    }

    public static BudgetProjection ToProjection(this BudgetValidation validation) =>
        new(validation.ProjectedMonthlyCalls, validation.MonthlyLimit, validation.UsagePercent,
            validation.WithinBudget, [.. validation.Warnings]);

    public static AlertSection ToSection(this AlertOptions a) =>
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

    public static IndexSection ToSection(this IndexOptions indices) =>
        new(
            indices.Enabled,
            indices.Preferences.ToValues(),
            indices.ScoreOverrides.ToValues(),
            [
                .. indices.LocationOverrides.Select(o => new LocationIndexPreferences(
                    o.Location, o.Preferences.ToValues(), o.ScoreOverrides.ToValues()))
            ]);

    public static Dictionary<string, IndexPreferenceValues> ToValues(this IDictionary<string, IndexPreferences> overrides) =>
        overrides.ToDictionary(p => p.Key, p => p.Value.ToValues(), StringComparer.OrdinalIgnoreCase);

    public static IndexPreferenceValues ToValues(this IndexPreferences p) =>
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
