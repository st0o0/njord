using Njord.Core;
using Njord.Core.Polling;
using Njord.Messages.Polling;
using static Njord.Polling.Tests.PollStateFixture;

namespace Njord.Polling.Tests;

public sealed class DomainPollStateTests
{
    private static readonly TimeSpan _interval = TimeSpan.FromMinutes(60);

    [Fact]
    public void A_changed_hash_schedules_next_poll_at_effective_interval_minus_buffer()
    {
        var domain = DomainPollState.Empty(DomainKind.Forecast, _interval);

        var updated = domain.RecordHashChange(42, T0);

        Assert.Multiple(
            () => Assert.Equal(PollPhase.Active, updated.Phase),
            () => Assert.Equal(42, updated.LastHash),
            () => Assert.Equal(T0, updated.LastChanged),
            () => Assert.Equal(T0 + _interval - DomainPollStateExtensions.SteadyBuffer, updated.NextPollAt));
    }

    [Fact]
    public void An_unchanged_hash_schedules_next_poll_at_effective_interval()
    {
        var domain = DomainPollState.Empty(DomainKind.Forecast, _interval)
            .RecordHashChange(42, T0);

        var updated = domain.RecordHashUnchanged(T0.AddMinutes(60));

        Assert.Multiple(
            () => Assert.Equal(PollPhase.Active, updated.Phase),
            () => Assert.Equal(T0.AddMinutes(60) + _interval, updated.NextPollAt));
    }

    [Fact]
    public void Domains_poll_independently()
    {
        var state = StartedAll()
            .Apply(Changed(DomainKind.Forecast, 1, T0))
            .Apply(Changed(DomainKind.Marine, 1, T0));

        var forecast = state.GetDomain(DomainKind.Forecast);
        var marine = state.GetDomain(DomainKind.Marine);
        Assert.NotEqual(forecast.NextPollAt, marine.NextPollAt);
    }

    [Fact]
    public void The_location_is_next_due_when_its_earliest_domain_is()
    {
        var state = StartedAll()
            .Apply(Changed(DomainKind.Forecast, 1, T0))
            .Apply(Changed(DomainKind.AirQuality, 1, T0))
            .Apply(Changed(DomainKind.Flood, 1, T0))
            .Apply(Changed(DomainKind.Marine, 1, T0));

        var earliest = state.Domains.Values.Min(d => d.NextPollAt);
        Assert.Equal(earliest, state.NextPollUtc);
    }

    [Fact]
    public void Only_domains_whose_schedule_has_come_are_due()
    {
        var state = StartedAll()
            .Apply(Changed(DomainKind.Forecast, 1, T0))
            .Apply(Changed(DomainKind.Flood, 1, T0.AddMinutes(10)));
        DomainKind[] enabled = [DomainKind.Forecast, DomainKind.Flood, DomainKind.AirQuality];

        var dueAt5 = state.DueDomainsAt(enabled, T0 + TimeSpan.FromMinutes(5));
        Assert.Equal([DomainKind.AirQuality], dueAt5);
    }

    [Fact]
    public void A_changed_event_resets_consecutive_failures()
    {
        var domain = DomainPollState.Empty(DomainKind.Forecast, _interval) with { ConsecutiveFailures = 3 };

        var updated = domain.RecordHashChange(42, T0);

        Assert.Equal(0, updated.ConsecutiveFailures);
    }

    [Fact]
    public void An_unchanged_event_resets_consecutive_failures()
    {
        var domain = DomainPollState.Empty(DomainKind.Forecast, _interval) with { ConsecutiveFailures = 3 };

        var updated = domain.RecordHashUnchanged(T0);

        Assert.Equal(0, updated.ConsecutiveFailures);
    }

    // --- Drift ---

    [Fact]
    public void First_change_does_not_trigger_drift()
    {
        var domain = DomainPollState.Empty(DomainKind.Forecast, _interval)
            .RecordHashChange(1, T0);

        Assert.Multiple(
            () => Assert.Null(domain.ObservedIntervalEma),
            () => Assert.Equal(0, domain.DriftObservationCount),
            () => Assert.Equal(_interval, domain.EffectiveInterval));
    }

    [Fact]
    public void Second_change_starts_tracking_but_does_not_drift_yet()
    {
        var domain = DomainPollState.Empty(DomainKind.Forecast, _interval)
            .RecordHashChange(1, T0)
            .RecordHashChange(2, T0.AddMinutes(30));

        Assert.Multiple(
            () => Assert.NotNull(domain.ObservedIntervalEma),
            () => Assert.Equal(1, domain.DriftObservationCount),
            () => Assert.Equal(_interval, domain.EffectiveInterval));
    }

    [Fact]
    public void Three_observations_activate_drift()
    {
        var domain = DomainPollState.Empty(DomainKind.Forecast, _interval)
            .RecordHashChange(1, T0)
            .RecordHashChange(2, T0.AddMinutes(30))
            .RecordHashChange(3, T0.AddMinutes(60))
            .RecordHashChange(4, T0.AddMinutes(90));

        Assert.Multiple(
            () => Assert.Equal(3, domain.DriftObservationCount),
            () => Assert.True(domain.EffectiveInterval < _interval));
    }

    [Fact]
    public void Drift_is_clamped_at_half_the_configured_interval()
    {
        var domain = DomainPollState.Empty(DomainKind.Forecast, _interval);
        for (var i = 0; i < 20; i++)
        {
            domain = domain.RecordHashChange(i, T0.AddMinutes(i * 5));
        }

        Assert.Equal(TimeSpan.FromMinutes(30), domain.EffectiveInterval);
    }

    [Fact]
    public void Drift_is_clamped_at_double_the_configured_interval()
    {
        var domain = DomainPollState.Empty(DomainKind.Forecast, _interval);
        for (var i = 0; i < 20; i++)
        {
            domain = domain.RecordHashChange(i, T0.AddHours(i * 10));
        }

        Assert.Equal(TimeSpan.FromMinutes(120), domain.EffectiveInterval);
    }

    // --- Suspended ---

    [Fact]
    public void A_suspended_domain_recovers_to_active_on_data_change()
    {
        var state = Started()
            .Apply(NoData(DomainKind.Forecast, T0))
            .Apply(Changed(DomainKind.Forecast, 42, T0.AddHours(24)));
        var weather = state.GetDomain(DomainKind.Forecast);

        Assert.Multiple(
            () => Assert.Equal(PollPhase.Active, weather.Phase),
            () => Assert.Equal(42, weather.LastHash),
            () => Assert.Equal(0, weather.ConsecutiveFailures));
    }
}
