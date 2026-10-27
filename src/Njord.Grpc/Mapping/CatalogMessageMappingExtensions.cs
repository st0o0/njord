using Njord.Core;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Messages.Config;

namespace Njord.Grpc.Mapping;

public static class CatalogMessageMappingExtensions
{
    public static CatalogResult ToCatalog(this NjordOptions scope, FeatureRequirementsOptions requirements)
    {
        IReadOnlyList<string> effective = requirements.WeatherModels.IsDefaultOrEmpty
            ? [requirements.PrimaryWeatherModel]
            : [.. requirements.WeatherModels];
        var locations = scope.Locations
            .Select(location => new CatalogLocation(
                new Location(location.Name, location.Latitude.GetValueOrDefault(), location.Longitude.GetValueOrDefault()),
                effective))
            .ToList();

        return new CatalogResult(locations, [.. WeatherModelCatalog.All.Select(m => new CatalogModel(
            m.Id, m.DisplayName, m.Provider, m.Region, m.ResolutionKm, m.ForecastDays, effective.Contains(m.Id)))]);
    }
}
