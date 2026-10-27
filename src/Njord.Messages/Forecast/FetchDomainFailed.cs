using Njord.Core;
using Njord.Core.Domain;

namespace Njord.Messages.Forecast;

public sealed record FetchDomainFailed(Location Location, DomainKind Domain, PollFailureReason Reason, string? Detail)
    : FetchDomainResponse;
