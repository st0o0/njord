using Njord.Forecast;

namespace Njord.Messages.Forecast;

public sealed record FetchDomainRejected(Location Location, DomainKind Domain, FetchRejectionReason Reason)
    : FetchDomainResponse;
