namespace Njord.Persistence.Events.Polling;

public sealed record PersistedPollWorkerState(
    string? Location,
    bool Active,
    TimeSpan DiscoveryInterval,
    IReadOnlyList<PersistedDomainState>? DomainStates);

public sealed record PersistedDomainState(
    int Domain,
    int? LastHash,
    PersistedPollPhase Phase = PersistedPollPhase.Discovery,
    TimeSpan? LearnedCycle = null,
    int MissCount = 0,
    DateTimeOffset? LastChanged = null,
    TimeSpan? CandidateCycle = null,
    DateTimeOffset? NextPollAt = null,
    int ConsecutiveFailures = 0);
