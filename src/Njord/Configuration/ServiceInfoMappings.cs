using Njord.Alerts.Configuration;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;
using Njord.Messages.Config;
using Njord.Messages.Pipeline;
using Njord.Model;

namespace Njord.Configuration;

public static class ServiceInfoMappings
{
    public static ServiceInfo ToServiceInfo(
        NjordOptions scope,
        PollingOptions polling,
        AlertOptions alerts) =>
        new(
            scope.ToCatalog(),
            BudgetCalculator.GetEffectiveBudget(polling).RequestsPerMonth,
            RequestBudget.OpenMeteoFreeTierDailyLimit,
            ActiveEnrichments(alerts));

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

    private static List<EnrichmentKind> ActiveEnrichments(AlertOptions alerts) =>
        alerts.Enabled ? [EnrichmentKind.Alerts] : [];
}
