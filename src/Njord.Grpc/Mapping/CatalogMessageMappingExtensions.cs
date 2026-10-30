using Njord.Core.Configuration;
using Njord.Messages.Config;

namespace Njord.Grpc.Mapping;

public static class CatalogMessageMappingExtensions
{
    public static CatalogResult ToCatalog(this NjordOptions scope, ActiveModels models)
    {
        var allModels = models.AllModels;
        var locations = scope.ResolvedLocations
            .Select(location => new CatalogLocation(location, allModels))
            .ToList();

        return new CatalogResult(locations, [.. allModels.Select(id => new CatalogModel(
            id, id, string.Empty, string.Empty, 0, 0, true))]);
    }
}
