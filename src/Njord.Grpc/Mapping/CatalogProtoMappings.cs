using System.Runtime.ExceptionServices;
using Njord.Grpc.V2;
using Njord.Messages.Config;
using CoverageTier = Njord.Model.CoverageTier;

namespace Njord.Grpc.Mapping;

internal static class CatalogProtoMappings
{
    public static CatalogResult ToResult(this CatalogResponse response)
    {
        switch (response)
        {
            case CatalogResult result:
                return result;
            case CatalogFailed failed:
                ExceptionDispatchInfo.Capture(failed.Cause).Throw();
                break;
        }

        throw new ArgumentOutOfRangeException(nameof(response), response.GetType().Name, "Unknown catalog response");
    }

    public static GetCatalogResponse ToProto(this CatalogResult catalog)
    {
        var response = new GetCatalogResponse();
        response.Locations.AddRange(catalog.Locations.Select(ToProto));
        response.Models.AddRange(catalog.Models.Select(ToProto));
        return response;
    }

    public static ModelInfo ToProto(this CatalogModel model)
    {
        var info = new ModelInfo { Id = model.Id };
        if (model.Coverage is not { } coverage)
        {
            return info;
        }

        info.DisplayName = coverage.DisplayName ?? model.Id;
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

    public static LocationInfo ToProto(this CatalogLocation location) =>
        new()
        {
            Name = location.Id.Name,
            Latitude = location.Latitude,
            Longitude = location.Longitude,
            Models = { location.Models },
        };
}
