using Njord.Weather;

namespace Njord.Messages.Config;

// The locations and models the running service serves, with each model's coverage (null when unknown).
public sealed record QueryCatalog;

public abstract record CatalogResponse;
public sealed record CatalogResult(IReadOnlyList<CatalogLocation> Locations, IReadOnlyList<CatalogModel> Models)
    : CatalogResponse;
public sealed record CatalogFailed(Exception Cause) : CatalogResponse;

public sealed record CatalogLocation(LocationId Id, double Latitude, double Longitude, IReadOnlyList<string> Models);

public sealed record CatalogModel(string Id, ModelCoverage? Coverage);
