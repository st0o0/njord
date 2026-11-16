using Njord.Core;

namespace Njord.Messages.Polling;

public sealed record DomainPollStateEntry(
    string Domain,
    PollPhase Phase,
    long? CycleSeconds,
    int MissCount,
    DateTimeOffset? NextPollAt);

public sealed record PollStateEntry(
    Location Location,
    PollPhase Phase,
    DateTimeOffset NextPollUtc,
    DateTimeOffset? LastChangeUtc,
    int MissCount,
    long? CycleSeconds,
    IReadOnlyList<DomainPollStateEntry>? DomainStates = null);
