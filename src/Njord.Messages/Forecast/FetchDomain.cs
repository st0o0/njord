using Njord.Forecast;
using Njord.Messages.Pipeline;

namespace Njord.Messages.Forecast;

public sealed record FetchDomain(Location Location, DomainKind Domain);

// Replies travel through the poll shard region, so every response routes by location name.
public abstract record FetchDomainResponse : IWithLocationId
{
    public abstract Location Location { get; init; }

    public abstract DomainKind Domain { get; init; }

    public string LocationName => Location.Name;
}
