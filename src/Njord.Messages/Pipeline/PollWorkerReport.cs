namespace Njord.Messages.Pipeline;

public enum PollWorkerOutcome
{
    Planned,
    Changed,
    Unchanged,
    Failed,
}

// Sent by a poll worker to the poll manager after every start and every fetch outcome.
public sealed record PollWorkerReport(PollTarget Target, PollWorkerOutcome Outcome, PollStateEntry State);
