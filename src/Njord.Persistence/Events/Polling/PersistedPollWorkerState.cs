namespace Njord.Persistence.Events.Polling;

public sealed record PersistedPollWorkerState(
    string? Location,
    string? ModelId,
    bool Active,
    TimeSpan DiscoveryInterval,
    int? LastHash,
    DateTimeOffset? LastChangeUtc,
    DateTimeOffset? PrevChangeUtc,
    DateTimeOffset NextPollUtc,
    int MissCount,
    PersistedPollPhase Phase,
    TimeSpan? Cycle,
    int TransientFailureCount);
