namespace Njord.Polling.Tests;

public sealed class FailureBackoffTests
{
    private static readonly TimeSpan _hour = TimeSpan.FromMinutes(60);

    [Theory]
    [InlineData(1, 60)]
    [InlineData(2, 120)]
    [InlineData(3, 240)]
    [InlineData(5, 960)]
    public void The_delay_doubles_with_each_consecutive_failure(int failures, int seconds) =>
        Assert.Equal(TimeSpan.FromSeconds(seconds), FailureBackoff.Delay(failures, _hour));

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    public void The_delay_is_capped_at_the_poll_interval(int failures) =>
        Assert.Equal(_hour, FailureBackoff.Delay(failures, _hour));

    [Fact]
    public void A_short_poll_interval_caps_the_first_delay() =>
        Assert.Equal(TimeSpan.FromSeconds(30), FailureBackoff.Delay(1, TimeSpan.FromSeconds(30)));

    [Fact]
    public void A_rate_limit_waits_at_least_five_minutes() =>
        Assert.Equal(PollWorkerState.RateLimitMinDelay, FailureBackoff.RateLimited(1, _hour));

    [Fact]
    public void A_rate_limit_keeps_the_longer_backoff() =>
        Assert.Equal(TimeSpan.FromMinutes(16), FailureBackoff.RateLimited(5, _hour));
}
