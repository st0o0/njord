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

public sealed record PersistedDomainState(int Domain, int? LastHash);
