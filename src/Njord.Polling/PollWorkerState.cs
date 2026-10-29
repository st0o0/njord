using System.Collections.Immutable;
using Njord.Core;
using Njord.Messages.Polling;

namespace Njord.Polling;

public sealed record DomainPollState(
    DomainKind Domain,
    PollPhase Phase,
    int? LastHash,
    DateTimeOffset? LastChanged,
    TimeSpan? CandidateCycle,
    TimeSpan? LearnedCycle,
    int MissCount,
    DateTimeOffset? NextPollAt,
    int ConsecutiveFailures = 0)
{
    public static DomainPollState Empty(DomainKind domain) =>
        new(domain, PollPhase.Discovery, null, null, null, null, 0, null);
}

public sealed record PollWorkerState(
    PollTarget? Target,
    bool Active,
    TimeSpan DiscoveryInterval,
    ImmutableDictionary<DomainKind, DomainPollState> Domains)
{
    public static readonly PollWorkerState Empty =
        new(null, false, TimeSpan.Zero, ImmutableDictionary<DomainKind, DomainPollState>.Empty);

    public static readonly TimeSpan RateLimitMinDelay = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan PipelineUnavailableRetry = TimeSpan.FromSeconds(5);

    // The location polls again when its earliest domain is due.
    public DateTimeOffset NextPollUtc
    {
        get
        {
            var upcoming = Domains.Values.Where(d => d.NextPollAt.HasValue).Select(d => d.NextPollAt!.Value).ToList();
            return upcoming.Count > 0 ? upcoming.Min() : DateTimeOffset.MinValue;
        }
    }
}
