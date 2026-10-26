using Njord.Forecast;
using Njord.Messages.Pipeline;

namespace Njord.Messages.Forecast;

public sealed record FetchDomain(Location Location, DomainKind Domain);

public abstract record FetchDomainResponse : IWithLocationId
{
    public abstract Location Location { get; init; }

    public abstract DomainKind Domain { get; init; }

    string IWithLocationId.LocationName => Location.Name;
}
