namespace Njord.Persistence.Events.Pipeline;

public sealed record PersistedPollSchedulerState(PersistedModelPollEntry[] Entries);

public sealed record PersistedModelPollEntry(
    string Key,
    int? LastHash,
    DateTimeOffset? LastChangeUtc,
    DateTimeOffset? PrevChangeUtc,
    int MissCount,
    int Phase,
    TimeSpan? Cycle,
    int TransientFailureCount);
