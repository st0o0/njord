using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Persistence.Events.Polling;
using Njord.Tests.Shared;

namespace Njord.Pipeline.Tests;

public sealed class PollWorkerStateSpec
{
    private static readonly DateTimeOffset T0 = TestTimeConstants.PipelineEpoch;
    private static readonly TimeSpan DiscoveryInterval = TimeSpan.FromMinutes(20);
    private static readonly int[] WeatherDomain = [(int)DomainKind.Weather];

    private static PollWorkerState Started(DateTimeOffset? at = null, TimeSpan? interval = null) =>
        PollWorkerState.Empty.Apply(new PollingStarted("lucerne", WeatherDomain, interval ?? DiscoveryInterval, at ?? T0));

    private static DomainPolled Changed(int hash, DateTimeOffset at) => new((int)DomainKind.Weather, 0, hash, at, null);
    private static DomainPolled Unchanged(DateTimeOffset at) => new((int)DomainKind.Weather, 1, null, at, null);
    private static DomainPolled Failed(DateTimeOffset at, TimeSpan? delay = null) => new((int)DomainKind.Weather, 2, null, at, delay ?? TimeSpan.FromMinutes(1));

    // Three changes at 3 h intervals: the domain sees two consistent intervals and transitions to Steady.
    private static PollWorkerState Steady() =>
        Started()
            .Apply(Changed(1, T0))
            .Apply(Changed(2, T0.AddHours(3)))
            .Apply(Changed(3, T0.AddHours(6)));

    [Fact]
    public void The_empty_state_is_not_started()
    {
        Assert.Multiple(
            () => Assert.Null(PollWorkerState.Empty.Target),
            () => Assert.False(PollWorkerState.Empty.Active));
    }

    [Fact]
    public void Initial_state_is_discovery_with_immediate_poll()
    {
        var state = Started();
        var weather = state.GetDomain(DomainKind.Weather);

        Assert.Multiple(
            () => Assert.Equal(new PollTarget(new Location("lucerne", 0, 0)), state.Target),
            () => Assert.True(state.Active),
            () => Assert.Equal(DiscoveryInterval, state.DiscoveryInterval),
            () => Assert.Equal(PollPhase.Discovery, weather.Phase),
            () => Assert.Equal(T0, state.NextPollUtc),
            () => Assert.Null(weather.LastHash));
    }

    [Fact]
    public void First_data_change_stays_in_discovery()
    {
        var state = Started().Apply(Changed(42, T0.AddMinutes(20)));
        var weather = state.GetDomain(DomainKind.Weather);

        Assert.Multiple(
            () => Assert.Equal(PollPhase.Discovery, weather.Phase),
            () => Assert.Equal(42, weather.LastHash),
            () => Assert.Equal(T0.AddMinutes(20), weather.LastChanged),
            () => Assert.Null(weather.LearnedCycle),
            () => Assert.Equal(T0.AddMinutes(40), weather.NextPollAt));
    }

    [Fact]
    public void Second_data_change_records_a_candidate_and_stays_in_discovery()
    {
        var state = Started()
            .Apply(Changed(42, T0.AddHours(1)))
            .Apply(Changed(99, T0.AddHours(4)));
        var weather = state.GetDomain(DomainKind.Weather);

        Assert.Multiple(
            () => Assert.Equal(PollPhase.Discovery, weather.Phase),
            () => Assert.Equal(TimeSpan.FromHours(3), weather.CandidateCycle),
            () => Assert.Null(weather.LearnedCycle));
    }

    [Fact]
    public void Third_consistent_change_computes_cycle_and_transitions_to_steady()
    {
        var weather = Steady().GetDomain(DomainKind.Weather);

        Assert.Multiple(
            () => Assert.Equal(PollPhase.Steady, weather.Phase),
            () => Assert.Equal(TimeSpan.FromHours(3), weather.LearnedCycle),
            () => Assert.Equal(T0.AddHours(6) + TimeSpan.FromHours(3) - DomainPollStateExtensions.SteadyBuffer,
                weather.NextPollAt));
    }

    [Fact]
    public void Retry_backoff_doubles_per_miss()
    {
        var state = Steady();
        var now = T0.AddHours(9);
        TimeSpan[] expected = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(8)];

        foreach (var delay in expected)
        {
            state = state.Apply(Unchanged(now));
            Assert.Equal(now + delay, state.GetDomain(DomainKind.Weather).NextPollAt);
            now = state.GetDomain(DomainKind.Weather).NextPollAt!.Value;
        }

        Assert.Equal(4, state.GetDomain(DomainKind.Weather).MissCount);
    }

    [Fact]
    public void Retry_backoff_never_exceeds_15_minutes()
    {
        var state = Steady();
        var now = T0.AddHours(9);
        for (var i = 0; i < 4; i++)
        {
            state = state.Apply(Unchanged(now));
            var nextAt = state.GetDomain(DomainKind.Weather).NextPollAt!.Value;
            Assert.True(nextAt - now <= TimeSpan.FromMinutes(15), $"miss {i + 1} waits {nextAt - now}");
            now = nextAt;
        }

        Assert.Equal(PollPhase.Steady, state.GetDomain(DomainKind.Weather).Phase);
    }

    [Fact]
    public void Five_misses_in_steady_falls_back_to_discovery()
    {
        var state = Steady();
        Assert.Equal(PollPhase.Steady, state.GetDomain(DomainKind.Weather).Phase);

        var now = T0.AddHours(9);
        var lastMiss = now;
        for (var i = 0; i < 5; i++)
        {
            lastMiss = now;
            state = state.Apply(Unchanged(now));
            now = state.GetDomain(DomainKind.Weather).NextPollAt!.Value;
        }

        var weather = state.GetDomain(DomainKind.Weather);
        Assert.Multiple(
            () => Assert.Equal(PollPhase.Discovery, weather.Phase),
            () => Assert.Null(weather.LearnedCycle),
            () => Assert.Equal(0, weather.MissCount),
            () => Assert.Equal(lastMiss + DiscoveryInterval, weather.NextPollAt));
    }

    [Fact]
    public void Discovery_misses_do_not_fall_back()
    {
        var state = Started();
        var now = T0;
        for (var i = 0; i < 10; i++)
        {
            state = state.Apply(Unchanged(now));
            var weather = state.GetDomain(DomainKind.Weather);
            Assert.Equal(PollPhase.Discovery, weather.Phase);
            Assert.Equal(now + DiscoveryInterval, weather.NextPollAt);
            now = weather.NextPollAt!.Value;
        }

        Assert.Equal(10, state.GetDomain(DomainKind.Weather).MissCount);
    }

    [Fact]
    public void Failures_preserve_phase_and_cycle()
    {
        var state = Steady();
        var now = T0.AddHours(9);
        for (var i = 0; i < 20; i++)
        {
            state = state.Apply(Failed(now));
            now = state.GetDomain(DomainKind.Weather).NextPollAt!.Value;
        }

        var weather = state.GetDomain(DomainKind.Weather);
        Assert.Multiple(
            () => Assert.Equal(PollPhase.Steady, weather.Phase),
            () => Assert.Equal(TimeSpan.FromHours(3), weather.LearnedCycle));
    }

    [Fact]
    public void Data_change_resets_miss_count()
    {
        var state = Steady();
        var now = T0.AddHours(9);
        for (var i = 0; i < 3; i++)
        {
            state = state.Apply(Unchanged(now));
            now = state.GetDomain(DomainKind.Weather).NextPollAt!.Value;
        }

        Assert.Equal(3, state.GetDomain(DomainKind.Weather).MissCount);

        state = state.Apply(Changed(4, now));
        Assert.Equal(0, state.GetDomain(DomainKind.Weather).MissCount);
    }
}
