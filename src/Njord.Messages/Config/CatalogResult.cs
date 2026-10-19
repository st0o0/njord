using Njord.Model;

namespace Njord.Messages.Config;

// The locations and models the running service serves, with each model's coverage (null when unknown).
public sealed record CatalogResult(IReadOnlyList<CatalogLocation> Locations, IReadOnlyList<CatalogModel> Models);

public sealed record CatalogLocation(Location Location, IReadOnlyList<string> Models);

public sealed record CatalogModel(string Id, ModelCoverage? Coverage);
