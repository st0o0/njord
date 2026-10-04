using Njord.Domain.Weather;

namespace Njord.Messages.Pipeline;

public sealed record HashResult(string Location, string ModelId, int Hash);

public sealed record ScheduledPoll(string Location, string ModelId);

public sealed record FetchFailed(string Location, string ModelId, FetchFailureReason Reason, string Detail);

public sealed record TriggerImmediatePoll(string Location, string Model);

public sealed record TriggerImmediatePollResult(int Count, List<string> Targets);

public sealed record QueryPollStates;

public sealed record PollStateEntry(
    string Location,
    string ModelId,
    PollPhase Phase,
    DateTimeOffset NextPollUtc,
    DateTimeOffset? LastChangeUtc,
    int MissCount,
    long? CycleSeconds);

public abstract record QueryPollStatesResponse;
public sealed record QueryPollStatesResult(IReadOnlyList<PollStateEntry> Entries) : QueryPollStatesResponse;
public sealed record QueryPollStatesFailed(Exception Cause) : QueryPollStatesResponse;
