using Njord.Grpc.V2;
using Njord.Weather;
using CoverageTier = Njord.Weather.CoverageTier;

namespace Njord.Grpc;

public static class CatalogMapper
{
    public static ModelInfo ToModelInfo(string modelId)
    {
        var coverage = ModelCoverageRegistry.Get(modelId);
        var info = new ModelInfo { Id = modelId };
        if (coverage is null)
        {
            return info;
        }

        info.DisplayName = coverage.DisplayName ?? modelId;
        info.Provider = coverage.Provider ?? "";
        info.Region = coverage.Region;
        info.CoverageTier = coverage.Tier switch
        {
            CoverageTier.Global => V2.CoverageTier.Global,
            CoverageTier.Continental => V2.CoverageTier.Continental,
            CoverageTier.Regional => V2.CoverageTier.Regional,
            _ => V2.CoverageTier.Unspecified,
        };
        if (coverage.MaxForecastHours.HasValue)
        {
            info.MaxForecastHours = coverage.MaxForecastHours.Value;
        }

        if (coverage.ResolutionKm.HasValue)
        {
            info.ResolutionKm = coverage.ResolutionKm.Value;
        }

        if (coverage.Description is not null)
        {
            info.Description = coverage.Description;
        }

        return info;
    }

    public static LocationInfo ToLocationInfo(LocationOptions location, IReadOnlyList<string> models)
    {
        return new LocationInfo
        {
            Name = location.Name,
            Latitude = location.Latitude,
            Longitude = location.Longitude,
            Models = { models },
        };
    }
}
