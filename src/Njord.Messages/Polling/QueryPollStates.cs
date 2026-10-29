namespace Njord.Messages.Polling;

public sealed record QueryPollStates;

public sealed record PollStatesResult(IReadOnlyList<PollStateEntry> Entries);
