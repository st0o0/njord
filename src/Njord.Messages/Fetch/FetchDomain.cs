using Njord.Core;
using Njord.Messages.Polling;

namespace Njord.Messages.Fetch;

public sealed record FetchDomain(Location Location, DomainKind Domain);

public abstract record FetchDomainResponse : IWithLocationId
{
    public abstract Location Location { get; init; }

    public abstract DomainKind Domain { get; init; }

    string IWithLocationId.LocationName => Location.Name;
}
