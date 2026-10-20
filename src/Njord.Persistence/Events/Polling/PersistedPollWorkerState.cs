namespace Njord.Persistence.Events.Polling;

public sealed record PersistedPollWorkerState(
    string? Location,
    bool Active,
    TimeSpan DiscoveryInterval,
    int? LastHash,
    DateTimeOffset? LastChangeUtc,
    DateTimeOffset? PrevChangeUtc,
    DateTimeOffset NextPollUtc,
    int MissCount,
    PersistedPollPhase Phase,
    TimeSpan? Cycle,
    int TransientFailureCount,
    IReadOnlyList<PersistedDomainState>? DomainStates);

public sealed record PersistedDomainState(
    int Domain,
    int? LastHash,
    PersistedPollPhase Phase = PersistedPollPhase.Discovery,
    TimeSpan? LearnedCycle = null,
    int MissCount = 0,
    DateTimeOffset? LastChanged = null,
    TimeSpan? CandidateCycle = null,
    DateTimeOffset? NextPollAt = null);
