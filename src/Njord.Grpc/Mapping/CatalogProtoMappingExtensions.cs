using Njord.Grpc.V2;
using Njord.Messages.Config;

namespace Njord.Grpc.Mapping;

internal static class CatalogProtoMappingExtensions
{
    public static GetCatalogResponse ToProto(this CatalogResult catalog)
    {
        var response = new GetCatalogResponse();
        response.Locations.AddRange(catalog.Locations.Select(ToProto));
        response.Models.AddRange(catalog.Models.Select(ToProto));
        return response;
    }

    public static ModelInfo ToProto(this CatalogModel model) =>
        new() { Id = model.Id };

    public static LocationInfo ToProto(this CatalogLocation location) =>
        new()
        {
            Name = location.Location.Name,
            Latitude = location.Location.Latitude,
            Longitude = location.Location.Longitude,
            Models = { location.Models },
        };
}
