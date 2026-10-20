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
        AlertOptions alerts,
        EnrichmentOptions enrichments) =>
        new(
            scope.ToCatalog(),
            BudgetCalculator.GetEffectiveBudget(polling).RequestsPerMonth,
            RequestBudget.OpenMeteoFreeTierDailyLimit,
            ActiveEnrichments(alerts, enrichments));

    public const string EnsembleModelId = "ensemble_mean";

    public static CatalogResult ToCatalog(this NjordOptions scope)
    {
        IReadOnlyList<string> models = [EnsembleModelId];
        var locations = scope.Locations
            .Select(location => new CatalogLocation(new Location(location.Name, location.Latitude, location.Longitude), models))
            .ToList();

        return new CatalogResult(locations, [new CatalogModel(EnsembleModelId)]);
    }

    private static List<string> ActiveEnrichments(AlertOptions alerts, EnrichmentOptions enrichments)
    {
        var active = new List<string>();
        if (alerts.Enabled)
        {
            active.Add(EnrichmentNames.Alerts);
        }

        active.AddRange(enrichments.EnabledKeys().Order(StringComparer.Ordinal));
        return active;
    }
}
