using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Persistence.Events.Polling;
using Njord.Tests.Shared;
using Njord.Model;
using Njord.Forecast;

namespace Njord.Pipeline.Tests;

public sealed class PollWorkerStateSpec
{
    private static readonly DateTimeOffset T0 = TestTimeConstants.PipelineEpoch;
    private static readonly TimeSpan DiscoveryInterval = TimeSpan.FromMinutes(20);
    private static readonly LocationId Lucerne = new("lucerne");

    private static PollWorkerState Started(DateTimeOffset? at = null, TimeSpan? interval = null) =>
        PollWorkerState.Empty.Apply(new PollingStarted("lucerne", "icon_d2", interval ?? DiscoveryInterval, at ?? T0));

    // Two changes three hours apart: Steady with a 3 h cycle.
    private static PollWorkerState Steady() =>
        Started().Apply(new ForecastChanged(1, T0)).Apply(new ForecastChanged(2, T0.AddHours(3)));

    [Fact]
    public void The_empty_state_is_not_started()
    {
        Assert.Multiple(
            () => Assert.Null(PollWorkerState.Empty.Target),
            () => Assert.False(PollWorkerState.Empty.Active),
            () => Assert.Equal(PollPhase.Discovery, PollWorkerState.Empty.Phase));
    }

    [Fact]
    public void Initial_state_is_discovery_with_immediate_poll()
    {
        var state = Started();

        Assert.Multiple(
            () => Assert.Equal(new PollTarget(Lucerne, "icon_d2"), state.Target),
            () => Assert.True(state.Active),
            () => Assert.Equal(DiscoveryInterval, state.DiscoveryInterval),
            () => Assert.Equal(PollPhase.Discovery, state.Phase),
            () => Assert.Equal(T0, state.NextPollUtc),
            () => Assert.Null(state.Cycle),
            () => Assert.Null(state.LastHash));
    }

    [Fact]
    public void First_data_change_stays_in_discovery()
    {
        var state = Started().Apply(new ForecastChanged(42, T0.AddMinutes(20)));

        Assert.Multiple(
            () => Assert.Equal(PollPhase.Discovery, state.Phase),
            () => Assert.Equal(42, state.LastHash),
            () => Assert.Equal(T0.AddMinutes(20), state.LastChangeUtc),
            () => Assert.Null(state.PrevChangeUtc),
            () => Assert.Null(state.Cycle),
            () => Assert.Equal(T0.AddMinutes(40), state.NextPollUtc));
    }

    [Fact]
    public void Second_data_change_computes_cycle_and_transitions_to_steady()
    {
        var state = Started()
            .Apply(new ForecastChanged(42, T0.AddHours(1)))
            .Apply(new ForecastChanged(99, T0.AddHours(4)));

        Assert.Multiple(
            () => Assert.Equal(PollPhase.Steady, state.Phase),
            () => Assert.Equal(TimeSpan.FromHours(3), state.Cycle),
            () => Assert.Equal(T0.AddHours(1), state.PrevChangeUtc),
            () => Assert.Equal(T0.AddHours(4) + TimeSpan.FromHours(3) + TimeSpan.FromMinutes(1), state.NextPollUtc));
    }

    [Fact]
    public void Retry_backoff_doubles_per_miss()
    {
        var state = Steady();
        var now = T0.AddHours(6);
        TimeSpan[] expected = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(8)];

        foreach (var delay in expected)
        {
            state = state.Apply(new ForecastUnchanged(now));
            Assert.Equal(now + delay, state.NextPollUtc);
            now = state.NextPollUtc;
        }

        Assert.Equal(4, state.MissCount);
    }

    [Fact]
    public void Retry_backoff_never_exceeds_15_minutes()
    {
        var state = Steady();
        var now = T0.AddHours(6);
        for (var i = 0; i < 4; i++)
        {
            state = state.Apply(new ForecastUnchanged(now));
            Assert.True(state.NextPollUtc - now <= TimeSpan.FromMinutes(15), $"miss {i + 1} waits {state.NextPollUtc - now}");
            now = state.NextPollUtc;
        }

        Assert.Equal(PollPhase.Steady, state.Phase);
    }

    [Fact]
    public void Five_misses_in_steady_falls_back_to_discovery()
    {
        var state = Steady();
        Assert.Equal(PollPhase.Steady, state.Phase);

        var now = T0.AddHours(6);
        var lastMiss = now;
        for (var i = 0; i < 5; i++)
        {
            lastMiss = now;
            state = state.Apply(new ForecastUnchanged(now));
            now = state.NextPollUtc;
        }

        Assert.Multiple(
            () => Assert.Equal(PollPhase.Discovery, state.Phase),
            () => Assert.Null(state.Cycle),
            () => Assert.Equal(0, state.MissCount),
            () => Assert.Equal(lastMiss + DiscoveryInterval, state.NextPollUtc));
    }

    [Fact]
    public void Discovery_misses_do_not_fall_back()
    {
        var state = Started();
        var now = T0;
        for (var i = 0; i < 10; i++)
        {
            state = state.Apply(new ForecastUnchanged(now));
            Assert.Equal(PollPhase.Discovery, state.Phase);
            Assert.Equal(now + DiscoveryInterval, state.NextPollUtc);
            now = state.NextPollUtc;
        }

        Assert.Equal(10, state.MissCount);
    }

    [Fact]
    public void Transient_failures_do_not_increment_miss_count()
    {
        var state = Steady();
        var now = T0.AddHours(6);
        for (var i = 0; i < 10; i++)
        {
            state = state.Apply(new PollFailed(now, TimeSpan.Zero));
            now = state.NextPollUtc;
        }

        Assert.Multiple(
            () => Assert.Equal(0, state.MissCount),
            () => Assert.Equal(10, state.TransientFailureCount));
    }

    [Fact]
    public void Transient_failures_preserve_phase_and_cycle()
    {
        var state = Steady();
        var now = T0.AddHours(6);
        for (var i = 0; i < 20; i++)
        {
            state = state.Apply(new PollFailed(now, TimeSpan.Zero));
            now = state.NextPollUtc;
        }

        Assert.Multiple(
            () => Assert.Equal(PollPhase.Steady, state.Phase),
            () => Assert.Equal(TimeSpan.FromHours(3), state.Cycle));
    }

    [Fact]
    public void Transient_failure_backoff_then_caps_at_discovery_interval()
    {
        var state = Steady();
        var now = T0.AddHours(6);
        TimeSpan[] expected =
        [
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(8),
            DiscoveryInterval, DiscoveryInterval,
        ];

        foreach (var delay in expected)
        {
            state = state.Apply(new PollFailed(now, TimeSpan.Zero));
            Assert.Equal(now + delay, state.NextPollUtc);
            now = state.NextPollUtc;
        }
    }

    [Fact]
    public void Recovery_after_transient_failures_first_miss_has_miss_count_one()
    {
        var state = Steady();
        var now = T0.AddHours(6);
        for (var i = 0; i < 20; i++)
        {
            state = state.Apply(new PollFailed(now, TimeSpan.Zero));
            now = state.NextPollUtc;
        }

        var miss = state.Apply(new ForecastUnchanged(now));

        Assert.Multiple(
            () => Assert.Equal(1, miss.MissCount),
            () => Assert.Equal(PollPhase.Steady, miss.Phase),
            () => Assert.Equal(now + TimeSpan.FromMinutes(1), miss.NextPollUtc));
    }

    [Fact]
    public void Data_change_resets_transient_failure_count()
    {
        var state = Steady();
        var now = T0.AddHours(6);
        for (var i = 0; i < 10; i++)
        {
            state = state.Apply(new PollFailed(now, TimeSpan.Zero));
            now = state.NextPollUtc;
        }

        Assert.Equal(10, state.TransientFailureCount);

        state = state.Apply(new ForecastChanged(3, now));
        Assert.Multiple(
            () => Assert.Equal(0, state.TransientFailureCount),
            () => Assert.Equal(0, state.MissCount));
    }

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

        var replanned = state.Apply(new PollingStarted("lucerne", "icon_d2", TimeSpan.FromMinutes(5), T0.AddMinutes(1)));

        Assert.Multiple(
            () => Assert.Equal(T0.AddMinutes(5), replanned.NextPollUtc),
            () => Assert.Equal(TimeSpan.FromMinutes(5), replanned.DiscoveryInterval));
    }

    [Fact]
    public void A_replanned_poll_is_never_earlier_than_now()
    {
        var state = Started().Apply(new ForecastChanged(42, T0));

        var replanned = state.Apply(new PollingStarted("lucerne", "icon_d2", TimeSpan.FromMinutes(5), T0.AddMinutes(10)));

        Assert.Equal(T0.AddMinutes(10), replanned.NextPollUtc);
    }

    [Fact]
    public void A_longer_discovery_interval_moves_the_next_poll_out()
    {
        var state = Started().Apply(new ForecastChanged(42, T0));

        var replanned = state.Apply(new PollingStarted("lucerne", "icon_d2", TimeSpan.FromHours(1), T0));

        Assert.Equal(T0.AddHours(1), replanned.NextPollUtc);
    }

    [Fact]
    public void Steady_and_backoff_delays_are_not_replanned()
    {
        var steady = Steady();
        var backoff = Started().Apply(new PollFailed(T0, TimeSpan.Zero));

        Assert.Multiple(
            () => Assert.Equal(steady.NextPollUtc,
                steady.Apply(new PollingStarted("lucerne", "icon_d2", TimeSpan.FromHours(2), T0.AddHours(3))).NextPollUtc),
            () => Assert.Equal(backoff.NextPollUtc,
                backoff.Apply(new PollingStarted("lucerne", "icon_d2", TimeSpan.FromHours(2), T0)).NextPollUtc));
    }

    [Fact]
    public void A_start_with_the_same_interval_keeps_the_state()
    {
        var state = Steady();

        var restarted = state.Apply(new PollingStarted("lucerne", "icon_d2", DiscoveryInterval, T0.AddHours(4)));

        Assert.Equal(state, restarted);
    }

    [Fact]
    public void Stopping_deactivates_and_a_new_start_resumes_the_learned_state()
    {
        var stopped = Steady().Apply(new PollingStopped(T0.AddHours(4)));
        var resumed = stopped.Apply(new PollingStarted("lucerne", "icon_d2", DiscoveryInterval, T0.AddHours(5)));

        Assert.Multiple(
            () => Assert.False(stopped.Active),
            () => Assert.True(resumed.Active),
            () => Assert.Equal(TimeSpan.FromHours(3), resumed.Cycle),
            () => Assert.Equal(Steady().NextPollUtc, resumed.NextPollUtc));
    }

    [Fact]
    public void A_changed_hash_becomes_a_forecast_changed_event()
    {
        var state = Started().Apply(new ForecastChanged(42, T0));

        var (evt, outcome) = state.ToEvent(new FetchForecastCompleted(Lucerne, "icon_d2", 99), T0.AddHours(1));

        Assert.Multiple(
            () => Assert.Equal(new ForecastChanged(99, T0.AddHours(1)), evt),
            () => Assert.Equal(PollWorkerOutcome.Changed, outcome));
    }

    [Fact]
    public void The_first_hash_is_a_change()
    {
        var (evt, outcome) = Started().ToEvent(new FetchForecastCompleted(Lucerne, "icon_d2", 7), T0);

        Assert.Multiple(
            () => Assert.Equal(new ForecastChanged(7, T0), evt),
            () => Assert.Equal(PollWorkerOutcome.Changed, outcome));
    }

    [Fact]
    public void The_same_hash_becomes_a_forecast_unchanged_event()
    {
        var state = Started().Apply(new ForecastChanged(42, T0));

        var (evt, outcome) = state.ToEvent(new FetchForecastCompleted(Lucerne, "icon_d2", 42), T0.AddHours(1));

        Assert.Multiple(
            () => Assert.Equal(new ForecastUnchanged(T0.AddHours(1)), evt),
            () => Assert.Equal(PollWorkerOutcome.Unchanged, outcome));
    }

    [Theory]
    [InlineData(PollFailureReason.Transport, 0)]
    [InlineData(PollFailureReason.ModelUnavailable, 0)]
    [InlineData(PollFailureReason.MalformedPayload, 0)]
    [InlineData(PollFailureReason.RateLimited, 5)]
    public void A_failure_becomes_a_poll_failed_event_with_its_minimum_delay(PollFailureReason reason, int minimumMinutes)
    {
        var (evt, outcome) = Started().ToEvent(new FetchForecastFailed(Lucerne, "icon_d2", reason, "x"), T0);

        Assert.Multiple(
            () => Assert.Equal(new PollFailed(T0, TimeSpan.FromMinutes(minimumMinutes)), evt),
            () => Assert.Equal(PollWorkerOutcome.Failed, outcome));
    }

    [Theory]
    [InlineData(FetchRejectionReason.Budget, 5)]
    [InlineData(FetchRejectionReason.QueueFull, 0)]
    [InlineData(FetchRejectionReason.Expired, 0)]
    public void A_rejection_becomes_a_poll_failed_event_with_its_minimum_delay(FetchRejectionReason reason, int minimumMinutes)
    {
        var (evt, outcome) = Started().ToEvent(new FetchForecastRejected(Lucerne, "icon_d2", reason), T0);

        Assert.Multiple(
            () => Assert.Equal(new PollFailed(T0, TimeSpan.FromMinutes(minimumMinutes)), evt),
            () => Assert.Equal(PollWorkerOutcome.Failed, outcome));
    }

    [Fact]
    public void A_pipeline_unavailable_rejection_becomes_a_deferral()
    {
        var (evt, outcome) = Started().ToEvent(
            new FetchForecastRejected(Lucerne, "icon_d2", FetchRejectionReason.PipelineUnavailable), T0);

        Assert.Multiple(
            () => Assert.Equal(new PollDeferred(T0, TimeSpan.FromSeconds(5)), evt),
            () => Assert.Equal(PollWorkerOutcome.Failed, outcome));
    }
}
