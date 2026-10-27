using Njord.Core;

namespace Njord.Messages.Config;

// The locations and models the running service serves,.
public sealed record CatalogResult(IReadOnlyList<CatalogLocation> Locations, IReadOnlyList<CatalogModel> Models);

public sealed record CatalogLocation(Location Location, IReadOnlyList<string> Models);

public sealed record CatalogModel(string Id, string DisplayName, string Provider, string Region, int ResolutionKm, int ForecastDays, bool Enabled);
