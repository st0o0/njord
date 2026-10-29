using Njord.Core;
using Njord.Core.Domain;

namespace Njord.Messages.Fetch;

public sealed record FetchDomainFailed(Location Location, DomainKind Domain, PollFailureReason Reason, string? Detail)
    : FetchDomainResponse;
