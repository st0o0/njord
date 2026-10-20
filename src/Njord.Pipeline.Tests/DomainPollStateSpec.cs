using System.Collections.Immutable;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Persistence.Events.Polling;
using Njord.Tests.Shared;

namespace Njord.Pipeline.Tests;

public sealed class DomainPollStateSpec
{
    private static readonly DateTimeOffset T0 = TestTimeConstants.PipelineEpoch;
    private static readonly TimeSpan Discovery = TimeSpan.FromMinutes(20);
    private static readonly int[] AllDomains =
        [(int)DomainKind.Weather, (int)DomainKind.AirQuality, (int)DomainKind.Flood, (int)DomainKind.Marine];

    private static PollWorkerState Started() =>
        PollWorkerState.Empty.Apply(new PollingStarted("lucerne", AllDomains, Discovery, T0));

    private static RoundCompleted Changed(DomainKind domain, int hash, DateTimeOffset at) =>
        new([new DomainPollResult((int)domain, PersistedDomainPollOutcome.Changed, hash, null)], at);

    private static RoundCompleted Unchanged(DomainKind domain, DateTimeOffset at) =>
        new([new DomainPollResult((int)domain, PersistedDomainPollOutcome.Unchanged, null, null)], at);

    private static RoundCompleted Failed(DomainKind domain, DateTimeOffset at, TimeSpan delay) =>
        new([new DomainPollResult((int)domain, PersistedDomainPollOutcome.Failed, null, delay)], at);

    private static PollWorkerState SteadyAirQuality() =>
        new[] { 0, 1, 2 }.Aggregate(Started(), (s, i) => s.Apply(Changed(DomainKind.AirQuality, i, T0.AddHours(i))));

    [Fact]
    public void The_first_change_only_starts_the_measurement()
    {
        var domain = Started().Apply(Changed(DomainKind.AirQuality, 7, T0)).GetDomain(DomainKind.AirQuality);

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
        var state = Started()
            .Apply(Changed(DomainKind.AirQuality, 1, T0))
            .Apply(Changed(DomainKind.AirQuality, 2, T0.AddHours(1)));
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
        var state = Started()
            .Apply(Changed(DomainKind.Flood, 1, T0))
            .Apply(Changed(DomainKind.Flood, 2, T0.AddHours(10)))
            .Apply(Changed(DomainKind.Flood, 3, T0.AddHours(10 + 11)));

        Assert.Equal(PollPhase.Steady, state.GetDomain(DomainKind.Flood).Phase);
    }

    [Fact]
    public void Inconsistent_intervals_stay_in_discovery_with_the_newest_candidate()
    {
        var state = Started()
            .Apply(Changed(DomainKind.Flood, 1, T0))
            .Apply(Changed(DomainKind.Flood, 2, T0.AddHours(1)))
            .Apply(Changed(DomainKind.Flood, 3, T0.AddHours(4)));
        var domain = state.GetDomain(DomainKind.Flood);

        Assert.Multiple(
            () => Assert.Equal(PollPhase.Discovery, domain.Phase),
            () => Assert.Equal(TimeSpan.FromHours(3), domain.CandidateCycle));
    }

    [Fact]
    public void Domains_learn_independently()
    {
        var state = SteadyAirQuality()
            .Apply(Changed(DomainKind.Weather, 1, T0))
            .Apply(Changed(DomainKind.Weather, 2, T0.AddHours(6)));

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
            state = state.Apply(Unchanged(DomainKind.AirQuality, now));
            var domain = state.GetDomain(DomainKind.AirQuality);
            Assert.Equal(PollPhase.Steady, domain.Phase);
            Assert.Equal(i, domain.MissCount);
            now = domain.NextPollAt!.Value;
        }

        state = state.Apply(Unchanged(DomainKind.AirQuality, now));
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
        var state = Started()
            .Apply(Changed(DomainKind.Marine, 1, T0))
            .Apply(Unchanged(DomainKind.Marine, T0 + Discovery));
        var domain = state.GetDomain(DomainKind.Marine);

        Assert.Multiple(
            () => Assert.Equal(1, domain.MissCount),
            () => Assert.Equal(T0 + Discovery + Discovery, domain.NextPollAt));
    }

    [Fact]
    public void A_failed_domain_retries_after_its_delay_and_keeps_its_learning()
    {
        var at = T0.AddHours(3);
        var state = SteadyAirQuality().Apply(Failed(DomainKind.AirQuality, at, TimeSpan.FromMinutes(5)));
        var domain = state.GetDomain(DomainKind.AirQuality);

        Assert.Multiple(
            () => Assert.Equal(PollPhase.Steady, domain.Phase),
            () => Assert.Equal(at.AddMinutes(5), domain.NextPollAt));
    }

    [Fact]
    public void The_location_is_next_due_when_its_earliest_domain_is()
    {
        // Advance every domain past T0 so the NextPollUtc reflects only the re-planned ones.
        var state = Started()
            .Apply(Changed(DomainKind.Weather, 1, T0))
            .Apply(Changed(DomainKind.AirQuality, 1, T0))
            .Apply(Changed(DomainKind.Flood, 1, T0))
            .Apply(Changed(DomainKind.Marine, 1, T0))
            .Apply(Changed(DomainKind.Weather, 2, T0.AddHours(6)))
            .Apply(Changed(DomainKind.Weather, 3, T0.AddHours(12)))
            .Apply(Changed(DomainKind.Flood, 2, T0.AddHours(12)));

        // Flood: 2 changes -> Discovery, NextPollAt = T0+12h + 20min.
        // AirQuality, Marine: 1 change -> Discovery, NextPollAt = T0 + 20min (earliest).
        Assert.Equal(T0 + Discovery, state.NextPollUtc);
    }

    [Fact]
    public void Only_domains_whose_schedule_has_come_are_due()
    {
        var state = Started()
            .Apply(Changed(DomainKind.Weather, 1, T0))
            .Apply(Changed(DomainKind.Flood, 1, T0.AddMinutes(10)));
        DomainKind[] enabled = [DomainKind.Weather, DomainKind.Flood, DomainKind.AirQuality];

        Assert.Multiple(
            () => Assert.Equal([DomainKind.AirQuality], state.DueDomainsAt(enabled, T0 + TimeSpan.FromMinutes(5))),
            () => Assert.Equal(
                [DomainKind.Weather, DomainKind.AirQuality],
                state.DueDomainsAt(enabled, T0 + Discovery)));
    }
}
