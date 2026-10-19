using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Persistence.Events.Polling;
using Njord.Tests.Shared;

namespace Njord.Pipeline.Tests;

public sealed class PollWorkerStateSpec
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
            () => Assert.Equal(new PollTarget(Lucerne), state.Target),
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
}
