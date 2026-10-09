namespace Njord.Messages.Pipeline;

public sealed record QueryPollStates;

public abstract record QueryPollStatesResponse;
public sealed record QueryPollStatesCompleted(IReadOnlyList<PollStateEntry> Entries) : QueryPollStatesResponse;
public sealed record QueryPollStatesFailed(Exception Cause) : QueryPollStatesResponse;
