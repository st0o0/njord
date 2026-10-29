using Njord.Core;
using Njord.Messages.Polling;
using static Njord.Polling.Tests.PollStateFixture;

namespace Njord.Polling.Tests;

public sealed class DomainPollStateTests
{
    [Fact]
    public void Intervals_within_twenty_percent_count_as_consistent()
    {
        var state = StartedAll()
            .Apply(Changed(DomainKind.Flood, 1, T0))
            .Apply(Changed(DomainKind.Flood, 2, T0.AddHours(10)))
            .Apply(Changed(DomainKind.Flood, 3, T0.AddHours(10 + 11)));

        Assert.Equal(PollPhase.Steady, state.GetDomain(DomainKind.Flood).Phase);
    }

    [Fact]
    public void Inconsistent_intervals_stay_in_discovery_with_the_newest_candidate()
    {
        var state = StartedAll()
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
        var state = Steady(DomainKind.AirQuality)
            .Apply(Changed(DomainKind.Forecast, 1, T0))
            .Apply(Changed(DomainKind.Forecast, 2, T0.AddHours(6)));

        Assert.Multiple(
            () => Assert.Equal(PollPhase.Steady, state.GetDomain(DomainKind.AirQuality).Phase),
            () => Assert.Equal(PollPhase.Discovery, state.GetDomain(DomainKind.Forecast).Phase));
    }

    [Fact]
    public void A_discovery_miss_waits_one_discovery_interval()
    {
        var state = StartedAll()
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
        var state = Steady(DomainKind.AirQuality).Apply(Failed(DomainKind.AirQuality, at, TimeSpan.FromMinutes(5)));
        var domain = state.GetDomain(DomainKind.AirQuality);

        Assert.Multiple(
            () => Assert.Equal(PollPhase.Steady, domain.Phase),
            () => Assert.Equal(at.AddMinutes(5), domain.NextPollAt));
    }

    [Fact]
    public void The_location_is_next_due_when_its_earliest_domain_is()
    {
        // Advance every domain past T0 so the NextPollUtc reflects only the re-planned ones.
        var state = StartedAll()
            .Apply(Changed(DomainKind.Forecast, 1, T0))
            .Apply(Changed(DomainKind.AirQuality, 1, T0))
            .Apply(Changed(DomainKind.Flood, 1, T0))
            .Apply(Changed(DomainKind.Marine, 1, T0))
            .Apply(Changed(DomainKind.Forecast, 2, T0.AddHours(6)))
            .Apply(Changed(DomainKind.Forecast, 3, T0.AddHours(12)))
            .Apply(Changed(DomainKind.Flood, 2, T0.AddHours(12)));

        // Flood: 2 changes -> Discovery, NextPollAt = T0+12h + 20min.
        // AirQuality, Marine: 1 change -> Discovery, NextPollAt = T0 + 20min (earliest).
        Assert.Equal(T0 + Discovery, state.NextPollUtc);
    }

    [Fact]
    public void Only_domains_whose_schedule_has_come_are_due()
    {
        var state = StartedAll()
            .Apply(Changed(DomainKind.Forecast, 1, T0))
            .Apply(Changed(DomainKind.Flood, 1, T0.AddMinutes(10)));
        DomainKind[] enabled = [DomainKind.Forecast, DomainKind.Flood, DomainKind.AirQuality];

        Assert.Multiple(
            () => Assert.Equal([DomainKind.AirQuality], state.DueDomainsAt(enabled, T0 + TimeSpan.FromMinutes(5))),
            () => Assert.Equal(
                [DomainKind.Forecast, DomainKind.AirQuality],
                state.DueDomainsAt(enabled, T0 + Discovery)));
    }
}
