namespace Njord.Polling;

internal static class FailureBackoff
{
    private static readonly TimeSpan Base = TimeSpan.FromSeconds(60);

    public static TimeSpan Delay(int consecutiveFailures, TimeSpan pollInterval)
    {
        if (consecutiveFailures < 1)
        {
            return Base < pollInterval ? Base : pollInterval;
        }

        var exponent = Math.Min(consecutiveFailures - 1, 20);
        var backoff = TimeSpan.FromSeconds(Base.TotalSeconds * (1L << exponent));
        return backoff < pollInterval ? backoff : pollInterval;
    }

    public static TimeSpan RateLimited(int consecutiveFailures, TimeSpan pollInterval)
    {
        var delay = Delay(consecutiveFailures, pollInterval);
        return delay > PollWorkerState.RateLimitMinDelay ? delay : PollWorkerState.RateLimitMinDelay;
    }
}
