namespace Njord.Messages.Polling;

public sealed record QueryPollStates;

public abstract record PollStatesResponse;
public sealed record PollStatesResult(IReadOnlyList<PollStateEntry> Entries) : PollStatesResponse;
public sealed record PollStatesFailed(Exception Cause) : PollStatesResponse;

