namespace Njord.Persistence.Events.Polling;

public sealed record PersistedPollWorkerState(
    string? Location,
    bool Active,
    List<PersistedDomainState>? DomainStates,
    double? Latitude = null,
    double? Longitude = null);

public sealed record PersistedDomainState(
    int Domain,
    int? LastHash,
    PersistedPollPhase Phase = PersistedPollPhase.Active,
    DateTimeOffset? LastChanged = null,
    DateTimeOffset? NextPollAt = null,
    int ConsecutiveFailures = 0,
    TimeSpan? PollInterval = null);
