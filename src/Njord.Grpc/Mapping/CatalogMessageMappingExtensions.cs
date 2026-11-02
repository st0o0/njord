using Njord.Core;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Messages.Config;

namespace Njord.Grpc.Mapping;

public static class CatalogMessageMappingExtensions
{
    public static CatalogResult ToCatalog(this NjordOptions scope, ActiveModels models, DomainRequirements requirements)
    {
        var allModels = models.AllModels;
        var resolvedDomains = BuildDomainSchemas(requirements);
        var locations = scope.ResolvedLocations
            .Select(location => new CatalogLocation(location, allModels, resolvedDomains))
            .ToList();

        return new CatalogResult(locations, [.. allModels.Select(id => new CatalogModel(
            id, id, string.Empty, string.Empty, 0, 0, true))]);
    }

    private static IReadOnlyList<CatalogDomainSchema> BuildDomainSchemas(DomainRequirements requirements)
    {
        var schemas = new List<CatalogDomainSchema>();

        if (requirements.AllForecast != default)
            schemas.Add(new CatalogDomainSchema(DomainKind.Forecast, ParamFieldNames.ResolveFields(requirements.AllForecast, ParamFieldNames.Forecast)));
        if (requirements.AllEnsemble != default)
            schemas.Add(new CatalogDomainSchema(DomainKind.Ensemble, ParamFieldNames.ResolveFields(requirements.AllEnsemble, ParamFieldNames.Ensemble)));
        if (requirements.AllAirQuality != default)
            schemas.Add(new CatalogDomainSchema(DomainKind.AirQuality, ParamFieldNames.ResolveFields(requirements.AllAirQuality, ParamFieldNames.AirQuality)));
        if (requirements.AllFlood != default)
            schemas.Add(new CatalogDomainSchema(DomainKind.Flood, ParamFieldNames.ResolveFields(requirements.AllFlood, ParamFieldNames.Flood)));
        if (requirements.AllMarine != default)
            schemas.Add(new CatalogDomainSchema(DomainKind.Marine, ParamFieldNames.ResolveFields(requirements.AllMarine, ParamFieldNames.Marine)));
        if (requirements.AllSeasonal != default)
            schemas.Add(new CatalogDomainSchema(DomainKind.Seasonal, ParamFieldNames.ResolveFields(requirements.AllSeasonal, ParamFieldNames.Seasonal)));

        return schemas;
    }
}
