using Njord.Core;

namespace Njord.Messages.Polling;

public sealed record DomainPollStateEntry(
    string Domain,
    PollPhase Phase,
    long? IntervalSeconds,
    DateTimeOffset? NextPollAt);

public sealed record PollStateEntry(
    Location Location,
    PollPhase Phase,
    DateTimeOffset NextPollUtc,
    DateTimeOffset? LastChangeUtc,
    IReadOnlyList<DomainPollStateEntry>? DomainStates = null);
