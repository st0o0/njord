using System.Collections.Immutable;
using Njord.Forecast;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Persistence.Events.Polling;

namespace Njord.Pipeline;

public sealed record DomainPollState(int? LastHash);

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
    ImmutableDictionary<DomainKind, DomainPollState> DomainStates)
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
    private const int MaxMissesBeforeFallback = 5;
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
                Target = new PollTarget(new LocationId(evt.Location)),
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

    // Update per-domain hash when a domain fetch completes.
    public static PollWorkerState WithDomainHash(this PollWorkerState state, DomainKind domain, int hash)
    {
        var domainState = new DomainPollState(hash);
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

    // The old overload for backward compat in existing fetch flow (gateway still uses FetchForecastResponse).
    public static (object Event, PollWorkerOutcome Outcome) ToEvent(
        this PollWorkerState state, FetchForecastResponse reply, DateTimeOffset now) => reply switch
        {
            FetchForecastCompleted completed when completed.Hash != state.LastHash =>
                (new ForecastChanged(completed.Hash, now), PollWorkerOutcome.Changed),
            FetchForecastCompleted => (new ForecastUnchanged(now), PollWorkerOutcome.Unchanged),
            FetchForecastFailed { Reason: PollFailureReason.RateLimited } =>
                (new PollFailed(now, PollWorkerState.RateLimitMinDelay), PollWorkerOutcome.Failed),
            FetchForecastRejected { Reason: FetchRejectionReason.Budget } =>
                (new PollFailed(now, PollWorkerState.RateLimitMinDelay), PollWorkerOutcome.Failed),
            FetchForecastRejected { Reason: FetchRejectionReason.PipelineUnavailable } =>
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
        _ => throw new ArgumentException($"Unknown poll worker event: {evt.GetType().Name}", nameof(evt)),
    };

    private static TimeSpan RetryBackoff(int count)
    {
        var delay = TimeSpan.FromMinutes(Math.Pow(2, count - 1));
        return delay > MaxRetryBackoff ? MaxRetryBackoff : delay;
    }
}
