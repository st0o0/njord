namespace Njord.Pipeline.Health;

public sealed record PollHealth(DateTimeOffset StartedUtc, DateTimeOffset? LastSuccessfulPollUtc);

public sealed class PollHealthState(TimeProvider timeProvider)
{
    private PollHealth _current = new(timeProvider.GetUtcNow(), null);

    public PollHealth Current => Volatile.Read(ref _current);

    public void RecordSuccessfulPoll(DateTimeOffset at) =>
        Volatile.Write(ref _current, Current with { LastSuccessfulPollUtc = at });
}
