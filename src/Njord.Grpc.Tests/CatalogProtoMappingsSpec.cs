using Njord.Grpc.Mapping;
using Njord.Messages.Config;
using Njord.Weather;

namespace Njord.Grpc.Tests;

public sealed class CatalogProtoMappingsSpec
{
    [Fact]
    public void Catalog_model_ToProto_should_map_registry_entry_fields()
    {
        var coverage = ModelCoverageRegistry.Get("icon_d2");
        Assert.NotNull(coverage);

        var info = new CatalogModel("icon_d2", coverage).ToProto();

        Assert.Multiple(
            () => Assert.Equal("icon_d2", info.Id),
            () => Assert.Equal(coverage.DisplayName ?? "icon_d2", info.DisplayName),
            () => Assert.Equal(coverage.Provider ?? "", info.Provider),
            () => Assert.Equal(coverage.Region, info.Region),
            () => Assert.Equal(coverage.MaxForecastHours ?? 0, info.MaxForecastHours),
            () => Assert.Equal(coverage.ResolutionKm ?? 0, info.ResolutionKm),
            () => Assert.Equal(coverage.Description ?? "", info.Description));
    }

    [Fact]
    public void Catalog_model_ToProto_should_leave_defaults_for_model_without_registry_entry()
    {
        var info = new CatalogModel("unknown_model_xyz", null).ToProto();

        Assert.Multiple(
            () => Assert.Equal("unknown_model_xyz", info.Id),
            () => Assert.Equal("", info.DisplayName),
            () => Assert.Equal("", info.Provider),
            () => Assert.Equal("", info.Region),
            () => Assert.Equal(V2.CoverageTier.Unspecified, info.CoverageTier));
    }

    [Theory]
    [InlineData("ecmwf_ifs025", CoverageTier.Global, V2.CoverageTier.Global)]
    [InlineData("icon_eu", CoverageTier.Continental, V2.CoverageTier.Continental)]
    [InlineData("icon_d2", CoverageTier.Regional, V2.CoverageTier.Regional)]
    public void Catalog_model_ToProto_should_map_coverage_tier(
        string modelId,
        CoverageTier domainTier,
        Njord.Grpc.V2.CoverageTier expected)
    {
        var coverage = ModelCoverageRegistry.Get(modelId);
        Assert.NotNull(coverage);
        Assert.Equal(domainTier, coverage.Tier);

        var info = new CatalogModel(modelId, coverage).ToProto();

        Assert.Equal(expected, info.CoverageTier);
    }

    [Fact]
    public void Catalog_location_ToProto_should_map_name_coordinates_and_models()
    {
        var location = new CatalogLocation(new LocationId("lucerne"), 47.05, 8.31, ["icon_d2", "ecmwf_ifs025"]);

        var info = location.ToProto();

        Assert.Multiple(
            () => Assert.Equal("lucerne", info.Name),
            () => Assert.Equal(47.05, info.Latitude),
            () => Assert.Equal(8.31, info.Longitude),
            () => Assert.Equal(["icon_d2", "ecmwf_ifs025"], info.Models));
    }
}
