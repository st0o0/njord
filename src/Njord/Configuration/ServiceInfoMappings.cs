using Njord.Alerts.Configuration;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;
using Njord.Derived.Configuration;
using Njord.Indices.Configuration;
using Njord.Messages.Config;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Pipeline.Configuration;
using Njord.Trends.Configuration;

namespace Njord.Configuration;

public static class ServiceInfoMappings
{
    public static ServiceInfo ToServiceInfo(
        NjordOptions scope,
        PollingOptions polling,
        ConsensusOptions consensus,
        AlertOptions alerts,
        DerivedOptions derived,
        TrendOptions trends,
        IndexOptions indices) =>
        new(
            scope.ToCatalog(),
            BudgetCalculator.GetEffectiveBudget(polling).RequestsPerMonth,
            RequestBudget.OpenMeteoFreeTierDailyLimit,
            ActiveEnrichments(consensus, alerts, derived, trends, indices));

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

    private static List<string> ResolveModels(NjordOptions scope, LocationOptions location) =>
        [.. scope.Models.Union(location.Models ?? [], StringComparer.OrdinalIgnoreCase)];

    private static List<EnrichmentKind> ActiveEnrichments(
        ConsensusOptions consensus,
        AlertOptions alerts,
        DerivedOptions derived,
        TrendOptions trends,
        IndexOptions indices)
    {
        var active = new List<EnrichmentKind>(5);
        if (consensus.Enabled)
        {
            active.Add(EnrichmentKind.Consensus);
        }

        if (alerts.Enabled)
        {
            active.Add(EnrichmentKind.Alerts);
        }

        if (derived.Enabled)
        {
            active.Add(EnrichmentKind.Derived);
        }

        if (trends.Enabled)
        {
            active.Add(EnrichmentKind.Trends);
        }

        if (indices.Enabled)
        {
            active.Add(EnrichmentKind.Indices);
        }

        return active;
    }
}
