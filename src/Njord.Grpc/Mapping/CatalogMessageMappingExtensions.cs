using Njord.Core;
using Njord.Core.Configuration;
using Njord.Messages.Config;

namespace Njord.Grpc.Mapping;

public static class CatalogMessageMappingExtensions
{
    public static CatalogResult ToCatalog(this NjordOptions scope, ActiveModels models)
    {
        var allModels = models.AllModels;
        var locations = scope.Locations
            .Select(location => new CatalogLocation(
                new Location(location.Name, location.Latitude.GetValueOrDefault(), location.Longitude.GetValueOrDefault()),
                allModels))
            .ToList();

        return new CatalogResult(locations, [.. allModels.Select(id => new CatalogModel(
            id, id, string.Empty, string.Empty, 0, 0, true))]);
    }
}
