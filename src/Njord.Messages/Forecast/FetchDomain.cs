using Njord.Model;

namespace Njord.Messages.Forecast;

public sealed record FetchDomain(Location Location, DomainKind Domain);

public abstract record FetchDomainResponse;
