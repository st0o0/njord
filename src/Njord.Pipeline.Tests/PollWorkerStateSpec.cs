using Njord.Forecast;
using Njord.Messages.Pipeline;
using Njord.Persistence.Events.Polling;
using static Njord.Pipeline.Tests.PollStateFixture;

namespace Njord.Pipeline.Tests;

public sealed class PollWorkerStateSpec
{
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
            () => Assert.Equal(Discovery, state.DiscoveryInterval),
            () => Assert.Equal(PollPhase.Discovery, weather.Phase),
            () => Assert.Equal(T0, state.NextPollUtc),
            () => Assert.Null(weather.LastHash));
    }

    [Fact]
    public void First_data_change_stays_in_discovery()
    {
        var state = Started().Apply(Changed(DomainKind.Weather, 42, T0.AddMinutes(20)));
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
            .Apply(Changed(DomainKind.Weather, 42, T0.AddHours(1)))
            .Apply(Changed(DomainKind.Weather, 99, T0.AddHours(4)));
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
            state = state.Apply(Unchanged(DomainKind.Weather, now));
            var next = state.GetDomain(DomainKind.Weather).NextPollAt;
            Assert.NotNull(next);
            Assert.Equal(now + delay, next);
            now = next.Value;
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
            state = state.Apply(Unchanged(DomainKind.Weather, now));
            var next = state.GetDomain(DomainKind.Weather).NextPollAt;
            Assert.NotNull(next);
            var nextAt = next.Value;
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
            state = state.Apply(Unchanged(DomainKind.Weather, now));
            var next = state.GetDomain(DomainKind.Weather).NextPollAt;
            Assert.NotNull(next);
            now = next.Value;
        }

        var weather = state.GetDomain(DomainKind.Weather);
        Assert.Multiple(
            () => Assert.Equal(PollPhase.Discovery, weather.Phase),
            () => Assert.Null(weather.LearnedCycle),
            () => Assert.Equal(0, weather.MissCount),
            () => Assert.Equal(lastMiss + Discovery, weather.NextPollAt));
    }

    [Fact]
    public void Discovery_misses_do_not_fall_back()
    {
        var state = Started();
        var now = T0;
        for (var i = 0; i < 10; i++)
        {
            state = state.Apply(Unchanged(DomainKind.Weather, now));
            var weather = state.GetDomain(DomainKind.Weather);
            Assert.Equal(PollPhase.Discovery, weather.Phase);
            Assert.Equal(now + Discovery, weather.NextPollAt);
            Assert.NotNull(weather.NextPollAt);
            now = weather.NextPollAt.Value;
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
            state = state.Apply(Failed(DomainKind.Weather, now));
            var next = state.GetDomain(DomainKind.Weather).NextPollAt;
            Assert.NotNull(next);
            now = next.Value;
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
            state = state.Apply(Unchanged(DomainKind.Weather, now));
            var next = state.GetDomain(DomainKind.Weather).NextPollAt;
            Assert.NotNull(next);
            now = next.Value;
        }

        Assert.Equal(3, state.GetDomain(DomainKind.Weather).MissCount);

        state = state.Apply(Changed(DomainKind.Weather, 4, now));
        Assert.Equal(0, state.GetDomain(DomainKind.Weather).MissCount);
    }
}
