using System.Collections.Immutable;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Persistence.Events.Polling;

namespace Njord.Pipeline;

public sealed record DomainPollState(
    int? LastHash,
    DateTimeOffset? LastChanged = null,
    TimeSpan? CandidateCycle = null,
    TimeSpan? LearnedCycle = null,
    PollPhase Phase = PollPhase.Discovery,
    int MissCount = 0,
    DateTimeOffset NextPollAt = default);

public sealed record PollWorkerState(
    PollTarget? Target,
    bool Active,
    TimeSpan DiscoveryInterval,
    int? LastHash,
    DateTimeOffset? LastChangeUtc,
    DateTimeOffset? PrevChangeUtc,
    DateTimeOffset NextPollUtc,
    int MissCount,
    PollPhase Phase,
    TimeSpan? Cycle,
    int TransientFailureCount,
    ImmutableDictionary<DomainKind, DomainPollState> DomainStates,
    PollRoundState? Round = null)
{
    public static readonly PollWorkerState Empty =
        new(null, false, TimeSpan.Zero, null, null, null, DateTimeOffset.MinValue, 0, PollPhase.Discovery, null, 0,
            ImmutableDictionary<DomainKind, DomainPollState>.Empty);

    public static readonly TimeSpan RateLimitMinDelay = TimeSpan.FromMinutes(5);

    // The gateway is (re-)attaching to the pipeline: nothing was fetched and nothing failed.
    public static readonly TimeSpan PipelineUnavailableRetry = TimeSpan.FromSeconds(5);
}

public static class PollWorkerStateExtensions
{
    private static readonly TimeSpan MaxRetryBackoff = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan SteadyBuffer = TimeSpan.FromMinutes(1);
    internal const int MaxMissesBeforeFallback = 5;
    private const int MaxTransientBeforeThrottle = 5;

    // The first start polls at once. A later start with another discovery interval re-plans a target that waits
    // out a discovery-phase delay: its next poll moves to (last plan - old interval + new interval), never before
    // now. Steady-phase targets follow their learned cycle and backoff delays stay as they are.
    public static PollWorkerState Apply(this PollWorkerState state, PollingStarted evt)
    {
        if (state.Target is null)
        {
            return state with
            {
                Target = new PollTarget(new Location(evt.Location, 0, 0)),
                Active = true,
                DiscoveryInterval = evt.DiscoveryInterval,
                NextPollUtc = evt.Utc,
            };
        }

        var next = state.NextPollUtc;
        if (evt.DiscoveryInterval != state.DiscoveryInterval
            && state is { Phase: PollPhase.Discovery, TransientFailureCount: 0 }
            && state.NextPollUtc > evt.Utc)
        {
            var replanned = state.NextPollUtc - state.DiscoveryInterval + evt.DiscoveryInterval;
            next = replanned < evt.Utc ? evt.Utc : replanned;
        }

        return state with { Active = true, DiscoveryInterval = evt.DiscoveryInterval, NextPollUtc = next };
    }

    public static PollWorkerState Apply(this PollWorkerState state, PollingStopped _) =>
        state with { Active = false };

    public static PollWorkerState Apply(this PollWorkerState state, ForecastChanged evt)
    {
        var cycle = state.LastChangeUtc is { } previous ? evt.Utc - previous : (TimeSpan?)null;
        return state with
        {
            LastHash = evt.Hash,
            PrevChangeUtc = state.LastChangeUtc,
            LastChangeUtc = evt.Utc,
            MissCount = 0,
            TransientFailureCount = 0,
            Phase = cycle is not null ? PollPhase.Steady : PollPhase.Discovery,
            Cycle = cycle ?? state.Cycle,
            NextPollUtc = cycle is { } learned ? evt.Utc + learned + SteadyBuffer : evt.Utc + state.DiscoveryInterval,
        };
    }

    public static PollWorkerState Apply(this PollWorkerState state, ForecastUnchanged evt)
    {
        var misses = state.MissCount + 1;
        if (state.Phase == PollPhase.Steady && misses >= MaxMissesBeforeFallback)
        {
            return state with
            {
                MissCount = 0,
                Phase = PollPhase.Discovery,
                Cycle = null,
                NextPollUtc = evt.Utc + state.DiscoveryInterval,
            };
        }

        var delay = state.Phase == PollPhase.Steady ? RetryBackoff(misses) : state.DiscoveryInterval;
        return state with { MissCount = misses, NextPollUtc = evt.Utc + delay };
    }

    public static PollWorkerState Apply(this PollWorkerState state, PollFailed evt)
    {
        var failures = state.TransientFailureCount + 1;
        var delay = failures >= MaxTransientBeforeThrottle ? state.DiscoveryInterval : RetryBackoff(failures);
        if (delay < evt.MinimumDelay)
        {
            delay = evt.MinimumDelay;
        }

        return state with { TransientFailureCount = failures, NextPollUtc = evt.Utc + delay };
    }

    public static PollWorkerState Apply(this PollWorkerState state, PollDeferred evt) =>
        state with { NextPollUtc = evt.Utc + evt.Delay };

    public static PollWorkerState Apply(this PollWorkerState state, DomainChanged evt)
    {
        var domain = (DomainKind)evt.Domain;
        var next = state.GetDomain(domain).RecordHashChange(evt.Hash, evt.Utc, state.DiscoveryInterval);
        return state.WithDomain(domain, next, evt.Utc);
    }

    public static PollWorkerState Apply(this PollWorkerState state, DomainUnchanged evt)
    {
        var domain = (DomainKind)evt.Domain;
        var next = state.GetDomain(domain).RecordHashUnchanged(evt.Utc, state.DiscoveryInterval);
        return state.WithDomain(domain, next, evt.Utc);
    }

    public static PollWorkerState Apply(this PollWorkerState state, DomainFailed evt)
    {
        var domain = (DomainKind)evt.Domain;
        var next = state.GetDomain(domain) with { NextPollAt = evt.Utc + evt.Delay };
        return state.WithDomain(domain, next, evt.Utc);
    }

    public static DomainPollState GetDomain(this PollWorkerState state, DomainKind domain) =>
        state.DomainStates.TryGetValue(domain, out var ds) ? ds : new DomainPollState(null);

    // The domains whose own schedule has come. A domain that was never polled is due.
    public static IReadOnlyList<DomainKind> DueDomainsAt(
        this PollWorkerState state, IEnumerable<DomainKind> enabled, DateTimeOffset now) =>
        [.. enabled.Where(d => !state.DomainStates.TryGetValue(d, out var ds) || ds.NextPollAt <= now)];

    // The location polls again when its earliest domain is due. Entries whose schedule already passed belong to
    // domains that are no longer polled (every polled domain was re-planned after `now`) and are ignored.
    private static PollWorkerState WithDomain(this PollWorkerState state, DomainKind domain, DomainPollState next,
        DateTimeOffset now)
    {
        var domains = state.DomainStates.SetItem(domain, next);
        var upcoming = domains.Values.Where(d => d.NextPollAt > now).Select(d => d.NextPollAt).ToList();
        return state with
        {
            DomainStates = domains,
            NextPollUtc = upcoming.Count > 0 ? upcoming.Min() : now + state.DiscoveryInterval,
        };
    }

    // The per-domain events of a finished round: every completed domain records a change or a miss, every other
    // domain is retried after its failure delay.
    public static IReadOnlyList<object> ToDomainEvents(
        this PollWorkerState state,
        IReadOnlyDictionary<DomainKind, FetchDomainResponse> results,
        DateTimeOffset now,
        TimeSpan allFailedDelay)
    {
        var anyCompleted = results.Values.Any(r => r is FetchDomainCompleted);
        var events = new List<object>(results.Count);
        foreach (var (domain, reply) in results.OrderBy(kv => kv.Key))
        {
            events.Add(reply switch
            {
                FetchDomainCompleted c when c.Hash != state.GetDomainHash(domain) =>
                    new DomainChanged((int)domain, c.Hash, now),
                FetchDomainCompleted => new DomainUnchanged((int)domain, now),
                _ => new DomainFailed((int)domain, now, anyCompleted ? FailureDelay(reply) : allFailedDelay),
            });
        }

        return events;
    }

    private static TimeSpan FailureDelay(FetchDomainResponse reply) => reply switch
    {
        FetchDomainFailed { Reason: PollFailureReason.RateLimited } => PollWorkerState.RateLimitMinDelay,
        FetchDomainRejected { Reason: FetchRejectionReason.Budget } => PollWorkerState.RateLimitMinDelay,
        FetchDomainRejected { Reason: FetchRejectionReason.PipelineUnavailable } =>
            PollWorkerState.PipelineUnavailableRetry,
        _ => TimeSpan.FromMinutes(1),
    };

    // Update per-domain hash when a domain fetch completes.
    public static PollWorkerState WithDomainHash(this PollWorkerState state, DomainKind domain, int hash)
    {
        var domainState = state.GetDomain(domain) with { LastHash = hash };
        return state with { DomainStates = state.DomainStates.SetItem(domain, domainState) };
    }

    public static int? GetDomainHash(this PollWorkerState state, DomainKind domain) =>
        state.DomainStates.TryGetValue(domain, out var ds) ? ds.LastHash : null;

    // The aggregate event a set of domain fetch replies leads to, and how the outcome counts for the poll cycle.
    public static (object Event, PollWorkerOutcome Outcome) ToEvent(
        this PollWorkerState state, FetchDomainResponse reply, DateTimeOffset now) => reply switch
        {
            FetchDomainCompleted completed when completed.Hash != state.GetDomainHash(completed.Domain) =>
                (new ForecastChanged(completed.Hash, now), PollWorkerOutcome.Changed),
            FetchDomainCompleted => (new ForecastUnchanged(now), PollWorkerOutcome.Unchanged),
            FetchDomainFailed { Reason: PollFailureReason.RateLimited } =>
                (new PollFailed(now, PollWorkerState.RateLimitMinDelay), PollWorkerOutcome.Failed),
            FetchDomainRejected { Reason: FetchRejectionReason.Budget } =>
                (new PollFailed(now, PollWorkerState.RateLimitMinDelay), PollWorkerOutcome.Failed),
            FetchDomainRejected { Reason: FetchRejectionReason.PipelineUnavailable } =>
                (new PollDeferred(now, PollWorkerState.PipelineUnavailableRetry), PollWorkerOutcome.Failed),
            _ => (new PollFailed(now, TimeSpan.Zero), PollWorkerOutcome.Failed),
        };

    public static PollWorkerState Apply(this PollWorkerState state, object evt) => evt switch
    {
        PollingStarted e => state.Apply(e),
        PollingStopped e => state.Apply(e),
        ForecastChanged e => state.Apply(e),
        ForecastUnchanged e => state.Apply(e),
        PollFailed e => state.Apply(e),
        PollDeferred e => state.Apply(e),
        DomainChanged e => state.Apply(e),
        DomainUnchanged e => state.Apply(e),
        DomainFailed e => state.Apply(e),
        _ => throw new ArgumentException($"Unknown poll worker event: {evt.GetType().Name}", nameof(evt)),
    };

    internal static TimeSpan RetryBackoff(int count)
    {
        var delay = TimeSpan.FromMinutes(Math.Pow(2, count - 1));
        return delay > MaxRetryBackoff ? MaxRetryBackoff : delay;
    }
}
