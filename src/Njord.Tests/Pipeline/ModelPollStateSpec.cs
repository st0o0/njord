using Njord.Pipeline;

namespace Njord.Tests.Pipeline;

public sealed class ModelPollStateSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 12, 6, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan DiscoveryInterval = TimeSpan.FromMinutes(20);

    [Fact(Timeout = 5000)]
    public void Initial_state_is_discovery_with_immediate_poll()
    {
        var state = ModelPollState.Initial(T0);
        Assert.Equal(PollPhase.Discovery, state.Phase);
        Assert.Equal(T0, state.NextPollUtc);
        Assert.Null(state.Cycle);
    }

    [Fact(Timeout = 5000)]
    public void First_data_change_stays_in_discovery()
    {
        var state = ModelPollState.Initial(T0)
            .WithDataChange(42, T0.AddMinutes(20), DiscoveryInterval);

        Assert.Equal(PollPhase.Discovery, state.Phase);
        Assert.Equal(42, state.LastHash);
        Assert.Equal(T0.AddMinutes(20), state.LastChangeUtc);
        Assert.Null(state.PrevChangeUtc);
        Assert.Null(state.Cycle);
        Assert.Equal(T0.AddMinutes(40), state.NextPollUtc);
    }

    [Fact(Timeout = 5000)]
    public void Second_data_change_computes_cycle_and_transitions_to_steady()
    {
        var state = ModelPollState.Initial(T0)
            .WithDataChange(42, T0.AddHours(1), DiscoveryInterval)
            .WithDataChange(99, T0.AddHours(4), DiscoveryInterval);

        Assert.Equal(PollPhase.Steady, state.Phase);
        Assert.Equal(TimeSpan.FromHours(3), state.Cycle);
        Assert.Equal(T0.AddHours(4) + TimeSpan.FromHours(3) + TimeSpan.FromMinutes(1), state.NextPollUtc);
    }

    [Fact(Timeout = 5000)]
    public void Retry_backoff_doubles_per_miss()
    {
        var state = ModelPollState.Initial(T0)
            .WithDataChange(1, T0, DiscoveryInterval)
            .WithDataChange(2, T0.AddHours(3), DiscoveryInterval);

        var miss1 = state.WithMiss(T0.AddHours(6), DiscoveryInterval);
        Assert.Equal(T0.AddHours(6) + TimeSpan.FromMinutes(1), miss1.NextPollUtc);

        var miss2 = miss1.WithMiss(T0.AddHours(6).AddMinutes(1), DiscoveryInterval);
        Assert.Equal(T0.AddHours(6).AddMinutes(1) + TimeSpan.FromMinutes(2), miss2.NextPollUtc);

        var miss3 = miss2.WithMiss(T0.AddHours(6).AddMinutes(3), DiscoveryInterval);
        Assert.Equal(T0.AddHours(6).AddMinutes(3) + TimeSpan.FromMinutes(4), miss3.NextPollUtc);

        var miss4 = miss3.WithMiss(T0.AddHours(6).AddMinutes(7), DiscoveryInterval);
        Assert.Equal(T0.AddHours(6).AddMinutes(7) + TimeSpan.FromMinutes(8), miss4.NextPollUtc);
    }

    [Fact(Timeout = 5000)]
    public void Retry_backoff_caps_at_15_minutes()
    {
        var state = ModelPollState.Initial(T0)
            .WithDataChange(1, T0, DiscoveryInterval)
            .WithDataChange(2, T0.AddHours(3), DiscoveryInterval);

        var now = T0.AddHours(6);
        for (var i = 0; i < 4; i++)
        {
            state = state.WithMiss(now, DiscoveryInterval);
            now = state.NextPollUtc;
        }

        var expectedCapped = state.NextPollUtc;
        var delay = expectedCapped - now + TimeSpan.FromMinutes(15);
        Assert.True(state.NextPollUtc - now <= TimeSpan.FromMinutes(15));
    }

    [Fact(Timeout = 5000)]
    public void Five_misses_in_steady_falls_back_to_discovery()
    {
        var state = ModelPollState.Initial(T0)
            .WithDataChange(1, T0, DiscoveryInterval)
            .WithDataChange(2, T0.AddHours(3), DiscoveryInterval);

        Assert.Equal(PollPhase.Steady, state.Phase);

        var now = T0.AddHours(6);
        for (var i = 0; i < 5; i++)
        {
            state = state.WithMiss(now, DiscoveryInterval);
            now = state.NextPollUtc;
        }

        Assert.Equal(PollPhase.Discovery, state.Phase);
        Assert.Null(state.Cycle);
        Assert.Equal(0, state.MissCount);
    }

    [Fact(Timeout = 5000)]
    public void Discovery_misses_do_not_fall_back()
    {
        var state = ModelPollState.Initial(T0);
        var now = T0;
        for (var i = 0; i < 10; i++)
        {
            state = state.WithMiss(now, DiscoveryInterval);
            Assert.Equal(PollPhase.Discovery, state.Phase);
            Assert.Equal(now + DiscoveryInterval, state.NextPollUtc);
            now = state.NextPollUtc;
        }
    }
}
