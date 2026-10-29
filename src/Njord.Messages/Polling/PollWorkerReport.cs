namespace Njord.Messages.Polling;

public enum PollWorkerOutcome
{
    Planned,
    Changed,
    Unchanged,
    Failed,
}

public sealed record PollWorkerReport(PollTarget Target, PollWorkerOutcome Outcome, PollStateEntry State);
