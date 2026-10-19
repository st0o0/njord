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

    // Locations in configuration order, each served by the configured models.
    public static CatalogResult ToCatalog(this NjordOptions scope)
    {
        var modelIds = scope.Models.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var locations = scope.Locations
            .Select(location => new CatalogLocation(new Location(location.Name, location.Latitude, location.Longitude), modelIds))
            .ToList();
        var models = modelIds
            .Select(modelId => new CatalogModel(modelId))
            .ToList();

        return new CatalogResult(locations, models);
    }

    private static List<string> ActiveEnrichments(AlertOptions alerts) =>
        alerts.Enabled ? [EnrichmentNames.Alerts] : [];
}
