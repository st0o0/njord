using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Persistence.Events.Polling;
using Njord.Tests.Shared;

namespace Njord.Pipeline.Tests;

public sealed class DomainPollStateSpec
{
    private static readonly DateTimeOffset T0 = TestTimeConstants.PipelineEpoch;
    private static readonly TimeSpan Discovery = TimeSpan.FromMinutes(20);
    private static readonly Location Lucerne = new("lucerne", 0, 0);

    private static PollWorkerState Started() =>
        PollWorkerState.Empty.Apply(new PollingStarted("lucerne", Discovery, T0));

    private static PollWorkerState Changed(PollWorkerState state, DomainKind domain, int hash, DateTimeOffset at) =>
        state.Apply(new DomainChanged((int)domain, hash, at));

    private static PollWorkerState SteadyAirQuality() =>
        new[] { 0, 1, 2 }.Aggregate(Started(), (s, i) => Changed(s, DomainKind.AirQuality, i, T0.AddHours(i)));

    [Fact]
    public void The_first_change_only_starts_the_measurement()
    {
        var domain = Changed(Started(), DomainKind.AirQuality, 7, T0).GetDomain(DomainKind.AirQuality);

        Assert.Multiple(
            () => Assert.Equal(7, domain.LastHash),
            () => Assert.Equal(T0, domain.LastChanged),
            () => Assert.Null(domain.CandidateCycle),
            () => Assert.Equal(PollPhase.Discovery, domain.Phase),
            () => Assert.Equal(T0 + Discovery, domain.NextPollAt));
    }

    [Fact]
    public void The_second_change_records_a_candidate_cycle()
    {
        var state = Changed(Changed(Started(), DomainKind.AirQuality, 1, T0), DomainKind.AirQuality, 2, T0.AddHours(1));
        var domain = state.GetDomain(DomainKind.AirQuality);

        Assert.Multiple(
            () => Assert.Equal(PollPhase.Discovery, domain.Phase),
            () => Assert.Equal(TimeSpan.FromHours(1), domain.CandidateCycle),
            () => Assert.Null(domain.LearnedCycle));
    }

    [Fact]
    public void Two_consistent_intervals_make_the_domain_steady()
    {
        var domain = SteadyAirQuality().GetDomain(DomainKind.AirQuality);

        Assert.Multiple(
            () => Assert.Equal(PollPhase.Steady, domain.Phase),
            () => Assert.Equal(TimeSpan.FromHours(1), domain.LearnedCycle),
            () => Assert.Equal(T0.AddHours(2) + TimeSpan.FromHours(1) - TimeSpan.FromSeconds(30), domain.NextPollAt));
    }

    [Fact]
    public void Intervals_within_twenty_percent_count_as_consistent()
    {
        var state = Changed(Started(), DomainKind.Flood, 1, T0);
        state = Changed(state, DomainKind.Flood, 2, T0.AddHours(10));
        state = Changed(state, DomainKind.Flood, 3, T0.AddHours(10 + 11));

        Assert.Equal(PollPhase.Steady, state.GetDomain(DomainKind.Flood).Phase);
    }

    [Fact]
    public void Inconsistent_intervals_stay_in_discovery_with_the_newest_candidate()
    {
        var state = Changed(Started(), DomainKind.Flood, 1, T0);
        state = Changed(state, DomainKind.Flood, 2, T0.AddHours(1));
        state = Changed(state, DomainKind.Flood, 3, T0.AddHours(4));
        var domain = state.GetDomain(DomainKind.Flood);

        Assert.Multiple(
            () => Assert.Equal(PollPhase.Discovery, domain.Phase),
            () => Assert.Equal(TimeSpan.FromHours(3), domain.CandidateCycle));
    }

    [Fact]
    public void Domains_learn_independently()
    {
        var state = SteadyAirQuality();
        state = Changed(state, DomainKind.Weather, 1, T0);
        state = Changed(state, DomainKind.Weather, 2, T0.AddHours(6));

        Assert.Multiple(
            () => Assert.Equal(PollPhase.Steady, state.GetDomain(DomainKind.AirQuality).Phase),
            () => Assert.Equal(PollPhase.Discovery, state.GetDomain(DomainKind.Weather).Phase));
    }

    [Fact]
    public void A_steady_domain_backs_off_on_misses_and_falls_back_after_five()
    {
        var state = SteadyAirQuality();
        var now = T0.AddHours(3);
        for (var i = 1; i <= 4; i++)
        {
            state = state.Apply(new DomainUnchanged((int)DomainKind.AirQuality, now));
            var domain = state.GetDomain(DomainKind.AirQuality);
            Assert.Equal(PollPhase.Steady, domain.Phase);
            Assert.Equal(i, domain.MissCount);
            now = domain.NextPollAt;
        }

        state = state.Apply(new DomainUnchanged((int)DomainKind.AirQuality, now));
        var fallen = state.GetDomain(DomainKind.AirQuality);

        Assert.Multiple(
            () => Assert.Equal(PollPhase.Discovery, fallen.Phase),
            () => Assert.Null(fallen.LearnedCycle),
            () => Assert.Equal(0, fallen.MissCount),
            () => Assert.Equal(now + Discovery, fallen.NextPollAt));
    }

    [Fact]
    public void A_discovery_miss_waits_one_discovery_interval()
    {
        var state = Changed(Started(), DomainKind.Marine, 1, T0)
            .Apply(new DomainUnchanged((int)DomainKind.Marine, T0 + Discovery));
        var domain = state.GetDomain(DomainKind.Marine);

        Assert.Multiple(
            () => Assert.Equal(1, domain.MissCount),
            () => Assert.Equal(T0 + Discovery + Discovery, domain.NextPollAt));
    }

    [Fact]
    public void A_failed_domain_retries_after_its_delay_and_keeps_its_learning()
    {
        var at = T0.AddHours(3);
        var state = SteadyAirQuality().Apply(new DomainFailed((int)DomainKind.AirQuality, at, TimeSpan.FromMinutes(5)));
        var domain = state.GetDomain(DomainKind.AirQuality);

        Assert.Multiple(
            () => Assert.Equal(PollPhase.Steady, domain.Phase),
            () => Assert.Equal(at.AddMinutes(5), domain.NextPollAt),
            () => Assert.Equal(at.AddMinutes(5), state.NextPollUtc));
    }

    [Fact]
    public void The_location_is_next_due_when_its_earliest_domain_is()
    {
        var state = Changed(Started(), DomainKind.Weather, 1, T0);
        state = Changed(state, DomainKind.Weather, 2, T0.AddHours(6));
        state = Changed(state, DomainKind.Weather, 3, T0.AddHours(12));
        state = Changed(state, DomainKind.Flood, 1, T0.AddHours(12));

        Assert.Equal(T0.AddHours(12) + Discovery, state.NextPollUtc);
    }

    [Fact]
    public void Only_domains_whose_schedule_has_come_are_due()
    {
        var state = Changed(Started(), DomainKind.Weather, 1, T0);
        state = Changed(state, DomainKind.Flood, 1, T0.AddMinutes(10));
        DomainKind[] enabled = [DomainKind.Weather, DomainKind.Flood, DomainKind.AirQuality];

        Assert.Multiple(
            () => Assert.Equal([DomainKind.AirQuality], state.DueDomainsAt(enabled, T0 + TimeSpan.FromMinutes(5))),
            () => Assert.Equal(
                [DomainKind.Weather, DomainKind.AirQuality],
                state.DueDomainsAt(enabled, T0 + Discovery)));
    }

    [Fact]
    public void Domain_events_follow_each_reply()
    {
        var state = Changed(Started(), DomainKind.Weather, 5, T0);
        var results = new Dictionary<DomainKind, FetchDomainResponse>
        {
            [DomainKind.Weather] = new FetchDomainCompleted(Lucerne, DomainKind.Weather, 5),
            [DomainKind.AirQuality] = new FetchDomainCompleted(Lucerne, DomainKind.AirQuality, 9),
            [DomainKind.Flood] = new FetchDomainFailed(Lucerne, DomainKind.Flood, PollFailureReason.RateLimited, "429"),
        };

        var events = state.ToDomainEvents(results, T0.AddHours(1), TimeSpan.FromMinutes(2));

        Assert.Equal(
            [
                new DomainUnchanged((int)DomainKind.Weather, T0.AddHours(1)),
                new DomainChanged((int)DomainKind.AirQuality, 9, T0.AddHours(1)),
                new DomainFailed((int)DomainKind.Flood, T0.AddHours(1), TimeSpan.FromMinutes(5)),
            ],
            events.Cast<object>().ToArray());
    }

    [Fact]
    public void An_all_failed_round_retries_on_the_given_delay()
    {
        var results = new Dictionary<DomainKind, FetchDomainResponse>
        {
            [DomainKind.Weather] = new FetchDomainFailed(Lucerne, DomainKind.Weather, PollFailureReason.Transport, "x"),
        };

        var events = Started().ToDomainEvents(results, T0, TimeSpan.FromMinutes(2));

        Assert.Equal(
            new object[] { new DomainFailed((int)DomainKind.Weather, T0, TimeSpan.FromMinutes(2)) },
            events.ToArray());
    }
}
