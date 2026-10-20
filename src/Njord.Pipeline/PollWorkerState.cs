using System.Collections.Immutable;
using Njord.Forecast;
using Njord.Messages.Pipeline;
using Njord.Persistence.Events.Polling;

namespace Njord.Pipeline;

public sealed record DomainPollState(
    DomainKind Domain,
    PollPhase Phase,
    int? LastHash,
    DateTimeOffset? LastChanged,
    TimeSpan? CandidateCycle,
    TimeSpan? LearnedCycle,
    int MissCount,
    DateTimeOffset? NextPollAt)
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

public static class PollWorkerStateExtensions
{
    private static readonly TimeSpan MaxRetryBackoff = TimeSpan.FromMinutes(15);
    internal const int MaxMissesBeforeFallback = 5;

    public static PollWorkerState Apply(this PollWorkerState state, PollingStarted evt)
    {
        if (state.Target is null)
        {
            var domains = state.Domains;
            foreach (var d in evt.Domains)
            {
                var dk = (DomainKind)d;
                if (!domains.ContainsKey(dk))
                {
                    domains = domains.SetItem(dk, DomainPollState.Empty(dk) with { NextPollAt = evt.Utc });
                }
            }

            return state with
            {
                Target = new PollTarget(new Location(evt.Location, 0, 0)),
                Active = true,
                DiscoveryInterval = evt.DiscoveryInterval,
                Domains = domains,
            };
        }

        if (evt.DiscoveryInterval != state.DiscoveryInterval)
        {
            // Replan discovery-phase domains to the new interval
            var domains = state.Domains;
            foreach (var (dk, ds) in state.Domains)
            {
                if (ds is { Phase: PollPhase.Discovery, NextPollAt: { } nextPoll } && nextPoll > evt.Utc)
                {
                    var replanned = nextPoll - state.DiscoveryInterval + evt.DiscoveryInterval;
                    var next = replanned < evt.Utc ? evt.Utc : replanned;
                    domains = domains.SetItem(dk, ds with { NextPollAt = next });
                }
            }

            return state with { Active = true, DiscoveryInterval = evt.DiscoveryInterval, Domains = domains };
        }

        return state with { Active = true };
    }

    public static PollWorkerState Apply(this PollWorkerState state, PollingStopped _) =>
        state with { Active = false };

    public static PollWorkerState Apply(this PollWorkerState state, DomainPolled evt)
    {
        var domain = (DomainKind)evt.Domain;
        var ds = state.GetDomain(domain);

        ds = evt.Outcome switch
        {
            PersistedDomainPollOutcome.Changed => ds.RecordHashChange(evt.Hash!.Value, evt.Utc, state.DiscoveryInterval),
            PersistedDomainPollOutcome.Unchanged => ds.RecordHashUnchanged(evt.Utc, state.DiscoveryInterval),
            PersistedDomainPollOutcome.Failed => ds with { NextPollAt = evt.Utc + (evt.Delay ?? TimeSpan.FromMinutes(1)) },
            _ => throw new ArgumentOutOfRangeException(nameof(evt), $"Unknown outcome: {evt.Outcome}"),
        };

        return state with { Domains = state.Domains.SetItem(domain, ds) };
    }

    public static DomainPollState GetDomain(this PollWorkerState state, DomainKind domain) =>
        state.Domains.TryGetValue(domain, out var ds) ? ds : DomainPollState.Empty(domain);

    public static IReadOnlyList<DomainKind> DueDomainsAt(
        this PollWorkerState state, IEnumerable<DomainKind> enabled, DateTimeOffset now) =>
        [.. enabled.Where(d => !state.Domains.TryGetValue(d, out var ds) || !ds.NextPollAt.HasValue || ds.NextPollAt.Value <= now)];

    public static int? GetDomainHash(this PollWorkerState state, DomainKind domain) =>
        state.Domains.TryGetValue(domain, out var ds) ? ds.LastHash : null;

    internal static TimeSpan RetryBackoff(int count)
    {
        var delay = TimeSpan.FromMinutes(Math.Pow(2, count - 1));
        return delay > MaxRetryBackoff ? MaxRetryBackoff : delay;
    }
}
