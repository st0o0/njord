using Njord.Core;

namespace Njord.Messages.Fetch;

public sealed record FetchDomainCompleted(Location Location, DomainKind Domain, int Hash) : FetchDomainResponse;
