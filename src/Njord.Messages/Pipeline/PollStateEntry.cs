using Njord.Model;

namespace Njord.Messages.Pipeline;

public sealed record PollStateEntry(
    LocationId Location,
    string ModelId,
    PollPhase Phase,
    DateTimeOffset NextPollUtc,
    DateTimeOffset? LastChangeUtc,
    int MissCount,
    long? CycleSeconds);
