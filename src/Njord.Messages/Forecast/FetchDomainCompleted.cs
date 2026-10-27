using Njord.Core;

namespace Njord.Messages.Forecast;

public sealed record FetchDomainCompleted(Location Location, DomainKind Domain, int Hash) : FetchDomainResponse;
