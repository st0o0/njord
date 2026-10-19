using Njord.Model;

namespace Njord.Messages.Forecast;

public sealed record FetchDomainRejected(LocationId Location, DomainKind Domain, FetchRejectionReason Reason)
    : FetchDomainResponse;
