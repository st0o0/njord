namespace Njord.Messages.Pipeline;

public sealed record PollStateEntry(
    string Location,
    string ModelId,
    PollPhase Phase,
    DateTimeOffset NextPollUtc,
    DateTimeOffset? LastChangeUtc,
    int MissCount,
    long? CycleSeconds);
