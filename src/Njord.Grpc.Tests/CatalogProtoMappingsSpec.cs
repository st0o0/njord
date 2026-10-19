using Njord.Grpc.Mapping;
using Njord.Messages.Config;
using Njord.Model;

namespace Njord.Grpc.Tests;

public sealed class CatalogProtoMappingsSpec
{
    [Fact]
    public void Catalog_model_ToProto_should_map_id_only()
    {
        var info = new CatalogModel("icon_d2").ToProto();

        Assert.Multiple(
            () => Assert.Equal("icon_d2", info.Id),
            () => Assert.Equal("", info.DisplayName),
            () => Assert.Equal("", info.Region),
            () => Assert.Equal(V2.CoverageTier.Unspecified, info.CoverageTier));
    }

    [Fact]
    public void Catalog_location_ToProto_should_map_name_coordinates_and_models()
    {
        var location = new CatalogLocation(new Location("lucerne", 47.05, 8.31), ["icon_d2", "ecmwf_ifs025"]);

        var info = location.ToProto();

        Assert.Multiple(
            () => Assert.Equal("lucerne", info.Name),
            () => Assert.Equal(47.05, info.Latitude),
            () => Assert.Equal(8.31, info.Longitude),
            () => Assert.Equal(["icon_d2", "ecmwf_ifs025"], info.Models));
    }
}
