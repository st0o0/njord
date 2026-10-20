using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Messages.Config;
using Njord.Messages.Pipeline;

namespace Njord.Configuration;

public static class ServiceInfoMappings
{
    public static ServiceInfo ToServiceInfo(
        NjordOptions scope,
        PollingOptions polling,
        IEnumerable<IFeatureRule> features) =>
        new(
            scope.ToCatalog(),
            BudgetCalculator.GetEffectiveBudget(polling).RequestsPerMonth,
            RequestBudget.OpenMeteoFreeTierDailyLimit,
            ActiveEnrichments(features));

    public const string EnsembleModelId = "ensemble_mean";

    public static CatalogResult ToCatalog(this NjordOptions scope)
    {
        IReadOnlyList<string> models = [EnsembleModelId];
        var locations = scope.Locations
            .Select(location => new CatalogLocation(new Location(location.Name, location.Latitude.GetValueOrDefault(), location.Longitude.GetValueOrDefault()), models))
            .ToList();

        return new CatalogResult(locations, [new CatalogModel(EnsembleModelId)]);
    }

    private static List<string> ActiveEnrichments(IEnumerable<IFeatureRule> features) =>
        [.. features
            .Where(static rule => rule.Enabled)
            .Select(static rule => $"{rule.Descriptor.Category}:{rule.GetType().Name}")
            .Order(StringComparer.Ordinal)];
}
