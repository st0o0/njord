using Njord.Core.Configuration;
using Njord.Weather;
using Njord.Grpc.V2;

namespace Njord.Grpc.Tests;

public sealed class CatalogMapperSpec
{
    [Fact]
    public void ToModelInfo_should_map_registry_entry_fields()
    {
        var coverage = ModelCoverageRegistry.Get("icon_d2");
        Assert.NotNull(coverage);

        var info = CatalogMapper.ToModelInfo("icon_d2");

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
    public void ToModelInfo_should_leave_defaults_for_model_without_registry_entry()
    {
        var info = CatalogMapper.ToModelInfo("unknown_model_xyz");

        Assert.Multiple(
            () => Assert.Equal("unknown_model_xyz", info.Id),
            () => Assert.Equal("", info.DisplayName),
            () => Assert.Equal("", info.Provider),
            () => Assert.Equal("", info.Region),
            () => Assert.Equal(Njord.Grpc.V2.CoverageTier.Unspecified, info.CoverageTier));
    }

    [Theory]
    [InlineData("ecmwf_ifs025", Njord.Core.Configuration.CoverageTier.Global, Njord.Grpc.V2.CoverageTier.Global)]
    [InlineData("icon_eu", Njord.Core.Configuration.CoverageTier.Continental, Njord.Grpc.V2.CoverageTier.Continental)]
    [InlineData("icon_d2", Njord.Core.Configuration.CoverageTier.Regional, Njord.Grpc.V2.CoverageTier.Regional)]
    public void ToModelInfo_should_map_coverage_tier(
        string modelId,
        Njord.Core.Configuration.CoverageTier domainTier,
        Njord.Grpc.V2.CoverageTier expected)
    {
        var coverage = ModelCoverageRegistry.Get(modelId);
        Assert.NotNull(coverage);
        Assert.Equal(domainTier, coverage.Tier);

        var info = CatalogMapper.ToModelInfo(modelId);

        Assert.Equal(expected, info.CoverageTier);
    }

    [Fact]
    public void ToLocationInfo_should_map_name_coordinates_and_models()
    {
        var location = new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 };

        var info = CatalogMapper.ToLocationInfo(location, ["icon_d2", "ecmwf_ifs025"]);

        Assert.Multiple(
            () => Assert.Equal("lucerne", info.Name),
            () => Assert.Equal(47.05, info.Latitude),
            () => Assert.Equal(8.31, info.Longitude),
            () => Assert.Equal(["icon_d2", "ecmwf_ifs025"], info.Models));
    }
}
