using Njord.Model;

namespace Njord.Messages.Pipeline;

public sealed record PollStateEntry(
    LocationId Location,
    PollPhase Phase,
    DateTimeOffset NextPollUtc,
    DateTimeOffset? LastChangeUtc,
    int MissCount,
    long? CycleSeconds);
