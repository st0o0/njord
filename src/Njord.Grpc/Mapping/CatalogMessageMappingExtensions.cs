using Njord.Core;
using Njord.Core.Configuration;
using Njord.Messages.Config;

namespace Njord.Grpc.Mapping;

public static class CatalogMessageMappingExtensions
{
    public static CatalogResult ToCatalog(this NjordOptions scope, EnsembleOptions ensemble)
    {
        IReadOnlyList<string> effective = ensemble.Models.Length == 0
            ? [EnsembleOptions.DefaultModel]
            : [.. ensemble.Models];
        var locations = scope.Locations
            .Select(location => new CatalogLocation(
                new Location(location.Name, location.Latitude.GetValueOrDefault(), location.Longitude.GetValueOrDefault()),
                effective))
            .ToList();

        return new CatalogResult(locations, [.. effective.Select(id => new CatalogModel(
            id, id, string.Empty, string.Empty, 0, ensemble.ForecastDays, true))]);
    }
}
