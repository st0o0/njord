using Njord.Forecast;

namespace Njord.Messages.Pipeline;

public sealed record PollStateEntry(
    Location Location,
    PollPhase Phase,
    DateTimeOffset NextPollUtc,
    DateTimeOffset? LastChangeUtc,
    int MissCount,
    long? CycleSeconds);
