using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Messages.Config;

namespace Njord.Configuration;

public static class ServiceInfoMappingExtensions
{
    public static ServiceInfo ToServiceInfo(
        this NjordOptions scope,
        PollingOptions polling,
        IEnumerable<IFeatureRule> features,
        EffectiveWeatherModels models) =>
        new(
            scope.ToCatalog(models),
            polling.EffectiveBudget.RequestsPerMonth,
            polling.EffectiveBudget.DailyLimit,
            ActiveEnrichments(features));

    public static CatalogResult ToCatalog(this NjordOptions scope, EffectiveWeatherModels effective)
    {
        IReadOnlyList<string> models = [.. effective.Ids];
        var locations = scope.Locations
            .Select(location => new CatalogLocation(new Location(location.Name, location.Latitude.GetValueOrDefault(), location.Longitude.GetValueOrDefault()), models))
            .ToList();

        return new CatalogResult(locations, [.. WeatherModelCatalog.All.Select(m => new CatalogModel(
            m.Id, m.DisplayName, m.Provider, m.Region, m.ResolutionKm, m.ForecastDays, effective.Contains(m.Id)))]);
    }

    private static List<string> ActiveEnrichments(IEnumerable<IFeatureRule> features) =>
        [.. features
            .Where(static rule => rule.Enabled)
            .Select(static rule => $"{rule.Descriptor.Category}:{rule.Descriptor.Name}")
            .Order(StringComparer.Ordinal)];
}
