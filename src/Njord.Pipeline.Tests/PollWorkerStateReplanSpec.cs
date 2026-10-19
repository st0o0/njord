using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Persistence.Events.Polling;
using Njord.Tests.Shared;

namespace Njord.Pipeline.Tests;

public sealed class PollWorkerStateReplanSpec
{
    private static readonly DateTimeOffset T0 = TestTimeConstants.PipelineEpoch;
    private static readonly TimeSpan DiscoveryInterval = TimeSpan.FromMinutes(20);
    private static readonly Location Lucerne = new("lucerne", 0, 0);

    private static PollWorkerState Started(DateTimeOffset? at = null, TimeSpan? interval = null) =>
        PollWorkerState.Empty.Apply(new PollingStarted("lucerne", interval ?? DiscoveryInterval, at ?? T0));

    // Two changes three hours apart: Steady with a 3 h cycle.
    private static PollWorkerState Steady() =>
        Started().Apply(new ForecastChanged(1, T0)).Apply(new ForecastChanged(2, T0.AddHours(3)));

    [Fact]
    public void A_minimum_delay_raises_the_next_poll()
    {
        var state = Started().Apply(new PollFailed(T0, PollWorkerState.RateLimitMinDelay));

        Assert.Multiple(
            () => Assert.Equal(T0 + TimeSpan.FromMinutes(5), state.NextPollUtc),
            () => Assert.Equal(1, state.TransientFailureCount));
    }

    [Fact]
    public void A_shorter_minimum_delay_keeps_a_later_backoff_untouched()
    {
        var state = Started().Apply(new PollFailed(T0, TimeSpan.FromSeconds(30)));

        Assert.Equal(T0.AddMinutes(1), state.NextPollUtc);
    }

    [Fact]
    public void A_deferred_poll_retries_after_the_delay_without_counting_a_failure()
    {
        var state = Started().Apply(new PollFailed(T0, TimeSpan.Zero))
            .Apply(new PollDeferred(T0.AddMinutes(1), PollWorkerState.PipelineUnavailableRetry));

        Assert.Multiple(
            () => Assert.Equal(T0.AddMinutes(1) + TimeSpan.FromSeconds(5), state.NextPollUtc),
            () => Assert.Equal(1, state.TransientFailureCount),
            () => Assert.Equal(0, state.MissCount));
    }

    [Fact]
    public void A_shorter_discovery_interval_replans_a_pending_discovery_poll()
    {
        var state = Started().Apply(new ForecastChanged(42, T0));

        var replanned = state.Apply(new PollingStarted("lucerne", TimeSpan.FromMinutes(5), T0.AddMinutes(1)));

        Assert.Multiple(
            () => Assert.Equal(T0.AddMinutes(5), replanned.NextPollUtc),
            () => Assert.Equal(TimeSpan.FromMinutes(5), replanned.DiscoveryInterval));
    }

    [Fact]
    public void A_replanned_poll_is_never_earlier_than_now()
    {
        var state = Started().Apply(new ForecastChanged(42, T0));

        var replanned = state.Apply(new PollingStarted("lucerne", TimeSpan.FromMinutes(5), T0.AddMinutes(10)));

        Assert.Equal(T0.AddMinutes(10), replanned.NextPollUtc);
    }

    [Fact]
    public void A_longer_discovery_interval_moves_the_next_poll_out()
    {
        var state = Started().Apply(new ForecastChanged(42, T0));

        var replanned = state.Apply(new PollingStarted("lucerne", TimeSpan.FromHours(1), T0));

        Assert.Equal(T0.AddHours(1), replanned.NextPollUtc);
    }

    [Fact]
    public void Steady_and_backoff_delays_are_not_replanned()
    {
        var steady = Steady();
        var backoff = Started().Apply(new PollFailed(T0, TimeSpan.Zero));

        Assert.Multiple(
            () => Assert.Equal(steady.NextPollUtc,
                steady.Apply(new PollingStarted("lucerne", TimeSpan.FromHours(2), T0.AddHours(3))).NextPollUtc),
            () => Assert.Equal(backoff.NextPollUtc,
                backoff.Apply(new PollingStarted("lucerne", TimeSpan.FromHours(2), T0)).NextPollUtc));
    }

    [Fact]
    public void A_start_with_the_same_interval_keeps_the_state()
    {
        var state = Steady();

        var restarted = state.Apply(new PollingStarted("lucerne", DiscoveryInterval, T0.AddHours(4)));

        Assert.Equal(state, restarted);
    }

    [Fact]
    public void Stopping_deactivates_and_a_new_start_resumes_the_learned_state()
    {
        var stopped = Steady().Apply(new PollingStopped(T0.AddHours(4)));
        var resumed = stopped.Apply(new PollingStarted("lucerne", DiscoveryInterval, T0.AddHours(5)));

        Assert.Multiple(
            () => Assert.False(stopped.Active),
            () => Assert.True(resumed.Active),
            () => Assert.Equal(TimeSpan.FromHours(3), resumed.Cycle),
            () => Assert.Equal(Steady().NextPollUtc, resumed.NextPollUtc));
    }

}
