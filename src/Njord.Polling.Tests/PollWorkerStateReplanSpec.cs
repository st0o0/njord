using Njord.Forecast;
using Njord.Messages.Pipeline;
using Njord.Persistence.Events.Polling;
using Njord.Tests.Shared;

namespace Njord.Polling.Tests;

public sealed class PollWorkerStateReplanSpec
{
    private static readonly DateTimeOffset T0 = TestTimeConstants.PipelineEpoch;
    private static readonly TimeSpan DiscoveryInterval = TimeSpan.FromMinutes(20);
    private static readonly int[] WeatherDomain = [(int)DomainKind.Weather];

    private static PollWorkerState Started(DateTimeOffset? at = null, TimeSpan? interval = null) =>
        PollWorkerState.Empty.Apply(new PollingStarted("lucerne", WeatherDomain, interval ?? DiscoveryInterval, at ?? T0));

    private static DomainPolled Changed(int hash, DateTimeOffset at) =>
        new((int)DomainKind.Weather, PersistedDomainPollOutcome.Changed, hash, at, null);

    // Three changes at 3 h intervals: two consistent intervals -> Steady.
    private static PollWorkerState Steady() =>
        Started()
            .Apply(Changed(1, T0))
            .Apply(Changed(2, T0.AddHours(3)))
            .Apply(Changed(3, T0.AddHours(6)));

    [Fact]
    public void A_shorter_discovery_interval_replans_a_pending_discovery_poll()
    {
        var state = Started().Apply(Changed(42, T0));

        var replanned = state.Apply(new PollingStarted("lucerne", WeatherDomain, TimeSpan.FromMinutes(5), T0.AddMinutes(1)));

        Assert.Multiple(
            () => Assert.Equal(T0.AddMinutes(5), replanned.GetDomain(DomainKind.Weather).NextPollAt),
            () => Assert.Equal(TimeSpan.FromMinutes(5), replanned.DiscoveryInterval));
    }

    [Fact]
    public void A_replanned_poll_is_never_earlier_than_now()
    {
        var state = Started().Apply(Changed(42, T0));

        var replanned = state.Apply(new PollingStarted("lucerne", WeatherDomain, TimeSpan.FromMinutes(5), T0.AddMinutes(10)));

        Assert.Equal(T0.AddMinutes(10), replanned.GetDomain(DomainKind.Weather).NextPollAt);
    }

    [Fact]
    public void A_longer_discovery_interval_moves_the_next_poll_out()
    {
        var state = Started().Apply(Changed(42, T0));

        var replanned = state.Apply(new PollingStarted("lucerne", WeatherDomain, TimeSpan.FromHours(1), T0));

        Assert.Equal(T0.AddHours(1), replanned.GetDomain(DomainKind.Weather).NextPollAt);
    }

    [Fact]
    public void Steady_delays_are_not_replanned()
    {
        var steady = Steady();
        var weatherBefore = steady.GetDomain(DomainKind.Weather).NextPollAt;

        var replanned = steady.Apply(new PollingStarted("lucerne", WeatherDomain, TimeSpan.FromHours(2), T0.AddHours(6)));
        var weatherAfter = replanned.GetDomain(DomainKind.Weather).NextPollAt;

        Assert.Equal(weatherBefore, weatherAfter);
    }

    [Fact]
    public void A_start_with_the_same_interval_keeps_the_state()
    {
        var state = Steady();

        var restarted = state.Apply(new PollingStarted("lucerne", WeatherDomain, DiscoveryInterval, T0.AddHours(7)));

        Assert.Equal(state.Domains, restarted.Domains);
    }

    [Fact]
    public void Stopping_deactivates_and_a_new_start_resumes_the_learned_state()
    {
        var stopped = Steady().Apply(new PollingStopped(T0.AddHours(7)));
        var resumed = stopped.Apply(new PollingStarted("lucerne", WeatherDomain, DiscoveryInterval, T0.AddHours(8)));

        Assert.Multiple(
            () => Assert.False(stopped.Active),
            () => Assert.True(resumed.Active),
            () => Assert.Equal(TimeSpan.FromHours(3), resumed.GetDomain(DomainKind.Weather).LearnedCycle),
            () => Assert.Equal(Steady().GetDomain(DomainKind.Weather).NextPollAt, resumed.GetDomain(DomainKind.Weather).NextPollAt));
    }
}
