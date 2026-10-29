using Njord.Core;

namespace Njord.Messages.Fetch;

public sealed record FetchDomainFailed(Location Location, DomainKind Domain, PollFailureReason Reason, string? Detail)
    : FetchDomainResponse;
