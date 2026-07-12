namespace Njord.Pipeline;

public enum PollPhase { Discovery, Steady }

public sealed record ModelPollState(
    int? LastHash,
    DateTimeOffset? LastChangeUtc,
    DateTimeOffset? PrevChangeUtc,
    DateTimeOffset NextPollUtc,
    int MissCount,
    PollPhase Phase,
    TimeSpan? Cycle)
{
    private static readonly TimeSpan MaxRetryBackoff = TimeSpan.FromMinutes(15);
    private const int MaxMissesBeforeFallback = 5;

    public static ModelPollState Initial(DateTimeOffset now) =>
        new(null, null, null, now, 0, PollPhase.Discovery, null);

    public ModelPollState WithDataChange(int hash, DateTimeOffset now, TimeSpan discoveryInterval)
    {
        var prevChange = LastChangeUtc;
        var cycle = prevChange is not null
            ? now - prevChange.Value
            : (TimeSpan?)null;
        var phase = cycle is not null ? PollPhase.Steady : PollPhase.Discovery;
        var nextPoll = cycle is not null
            ? now + cycle.Value + TimeSpan.FromMinutes(1)
            : now + discoveryInterval;

        return this with
        {
            LastHash = hash,
            PrevChangeUtc = prevChange,
            LastChangeUtc = now,
            MissCount = 0,
            Phase = phase,
            Cycle = cycle ?? Cycle,
            NextPollUtc = nextPoll,
        };
    }

    public ModelPollState WithMiss(DateTimeOffset now, TimeSpan discoveryInterval)
    {
        var newMissCount = MissCount + 1;

        if (Phase == PollPhase.Steady && newMissCount >= MaxMissesBeforeFallback)
        {
            return this with
            {
                MissCount = 0,
                Phase = PollPhase.Discovery,
                Cycle = null,
                NextPollUtc = now + discoveryInterval,
            };
        }

        var delay = Phase == PollPhase.Steady
            ? RetryBackoff(newMissCount)
            : discoveryInterval;

        return this with
        {
            MissCount = newMissCount,
            NextPollUtc = now + delay,
        };
    }

    private static TimeSpan RetryBackoff(int missCount)
    {
        var delay = TimeSpan.FromMinutes(Math.Pow(2, missCount - 1));
        return delay > MaxRetryBackoff ? MaxRetryBackoff : delay;
    }
}
