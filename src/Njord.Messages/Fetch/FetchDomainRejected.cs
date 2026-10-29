using Njord.Core;

namespace Njord.Messages.Fetch;

public sealed record FetchDomainRejected(Location Location, DomainKind Domain, FetchRejectionReason Reason)
    : FetchDomainResponse;
