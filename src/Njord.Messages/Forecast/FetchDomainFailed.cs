using Njord.Model;

namespace Njord.Messages.Forecast;

public sealed record FetchDomainFailed(Location Location, DomainKind Domain, PollFailureReason Reason, string? Detail)
    : FetchDomainResponse;
