using Njord.Core;
using Njord.Core.Polling;
using Njord.Messages.Polling;
using Njord.Persistence.Events.Polling;
using static Njord.Polling.Tests.PollStateFixture;

namespace Njord.Polling.Tests;

public sealed class PollWorkerStateTests
{
    private static readonly TimeSpan _forecastInterval = TimeSpan.FromMinutes(60);

    // --- Initial state ---

    [Fact]
    public void The_empty_state_is_not_started()
    {
        Assert.Multiple(
            () => Assert.Null(PollWorkerState.Empty.Target),
            () => Assert.False(PollWorkerState.Empty.Active));
    }

    [Fact]
    public void Initial_state_is_active_with_immediate_poll()
    {
        var state = Started();
        var weather = state.GetDomain(DomainKind.Forecast);

        Assert.Multiple(
            () => Assert.Equal(new PollTarget(Lucerne), state.Target),
            () => Assert.True(state.Active),
            () => Assert.Equal(PollPhase.Active, weather.Phase),
            () => Assert.Equal(T0, state.NextPollUtc),
            () => Assert.Null(weather.LastHash),
            () => Assert.Equal(_forecastInterval, weather.PollInterval));
    }

    // --- Changed / Unchanged event effects ---

    [Fact]
    public void A_changed_event_updates_the_hash_and_last_changed()
    {
        var state = Started().Apply(Changed(DomainKind.Forecast, 42, T0.AddMinutes(20)));
        var weather = state.GetDomain(DomainKind.Forecast);

        Assert.Multiple(
            () => Assert.Equal(42, weather.LastHash),
            () => Assert.Equal(T0.AddMinutes(20), weather.LastChanged));
    }

    [Fact]
    public void An_unchanged_event_schedules_next_poll_at_effective_interval()
    {
        var state = Started()
            .Apply(Changed(DomainKind.Forecast, 42, T0))
            .Apply(Unchanged(DomainKind.Forecast, T0.AddMinutes(60)));
        var weather = state.GetDomain(DomainKind.Forecast);

        Assert.Equal(T0.AddMinutes(60) + _forecastInterval, weather.NextPollAt);
    }

    [Fact]
    public void Data_change_resets_consecutive_failures()
    {
        var state = Started()
            .Apply(Failed(DomainKind.Forecast, T0, TimeSpan.FromMinutes(2)) with { ConsecutiveFailures = 2 })
            .Apply(Changed(DomainKind.Forecast, 42, T0.AddMinutes(2)));

        Assert.Equal(0, state.GetDomain(DomainKind.Forecast).ConsecutiveFailures);
    }

    // --- Failure events ---

    [Fact]
    public void A_failed_event_schedules_a_retry_at_the_given_delay()
    {
        var state = Started().Apply(Failed(DomainKind.Forecast, T0, TimeSpan.FromMinutes(5)));

        Assert.Equal(T0.AddMinutes(5), state.GetDomain(DomainKind.Forecast).NextPollAt);
    }

    [Fact]
    public void A_failed_event_records_the_consecutive_failure_count()
    {
        var evt = Failed(DomainKind.Forecast, T0, TimeSpan.FromMinutes(2)) with { ConsecutiveFailures = 2 };

        Assert.Equal(2, Started().Apply(evt).GetDomain(DomainKind.Forecast).ConsecutiveFailures);
    }

    [Fact]
    public void Failures_preserve_phase()
    {
        var state = Started();
        var now = T0;
        for (var i = 0; i < 20; i++)
        {
            state = state.Apply(Failed(DomainKind.Forecast, now));
            var next = state.GetDomain(DomainKind.Forecast).NextPollAt;
            Assert.NotNull(next);
            now = next.Value;
        }

        Assert.Equal(PollPhase.Active, state.GetDomain(DomainKind.Forecast).Phase);
    }

    [Fact]
    public void An_unknown_outcome_throws()
    {
        var evt = new DomainPolled((int)DomainKind.Forecast, (PersistedDomainPollOutcome)99, null, T0, null);

        Assert.Throws<ArgumentOutOfRangeException>(() => Started().Apply(evt));
    }

    // --- Per-domain intervals ---

    [Fact]
    public void Each_domain_carries_its_own_poll_interval()
    {
        var state = StartedAll();

        Assert.Multiple(
            () => Assert.Equal(DomainDefaults.PollInterval(DomainKind.Forecast), state.GetDomain(DomainKind.Forecast).PollInterval),
            () => Assert.Equal(DomainDefaults.PollInterval(DomainKind.Marine), state.GetDomain(DomainKind.Marine).PollInterval),
            () => Assert.Equal(DomainDefaults.PollInterval(DomainKind.Flood), state.GetDomain(DomainKind.Flood).PollInterval));
    }

    [Fact]
    public void A_start_with_different_intervals_updates_domains()
    {
        var state = Started(forecastInterval: TimeSpan.FromMinutes(30));

        Assert.Equal(TimeSpan.FromMinutes(30), state.GetDomain(DomainKind.Forecast).PollInterval);
    }

    // --- NoData / Suspended ---

    [Fact]
    public void A_no_data_event_transitions_to_suspended_with_24h_recheck()
    {
        var state = Started().Apply(NoData(DomainKind.Forecast, T0));
        var weather = state.GetDomain(DomainKind.Forecast);

        Assert.Multiple(
            () => Assert.Equal(PollPhase.Suspended, weather.Phase),
            () => Assert.Equal(T0 + PollWorkerState.SuspendedRecheckInterval, weather.NextPollAt),
            () => Assert.Equal(0, weather.ConsecutiveFailures));
    }

    [Fact]
    public void A_no_data_event_in_suspended_stays_suspended()
    {
        var firstNoData = T0;
        var secondNoData = firstNoData + PollWorkerState.SuspendedRecheckInterval;
        var state = Started()
            .Apply(NoData(DomainKind.Forecast, firstNoData))
            .Apply(NoData(DomainKind.Forecast, secondNoData));
        var weather = state.GetDomain(DomainKind.Forecast);

        Assert.Multiple(
            () => Assert.Equal(PollPhase.Suspended, weather.Phase),
            () => Assert.Equal(secondNoData + PollWorkerState.SuspendedRecheckInterval, weather.NextPollAt));
    }

    [Fact]
    public void A_changed_event_on_a_suspended_domain_resets_to_active()
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

    // --- Snapshot roundtrip for failure count ---

    [Fact]
    public void The_consecutive_failure_count_survives_a_snapshot_round_trip()
    {
        var state = Started().Apply(Failed(DomainKind.Forecast, T0, TimeSpan.FromMinutes(4)) with { ConsecutiveFailures = 3 });

        var restored = state.GetPersistenceState().FromPersistence();

        Assert.Equal(3, restored.GetDomain(DomainKind.Forecast).ConsecutiveFailures);
    }

    [Fact]
    public void A_persisted_domain_without_a_failure_count_recovers_as_zero()
    {
        var legacy = new PersistedPollWorkerState("lucerne", true,
            [new PersistedDomainState((int)DomainKind.Forecast, 7)]);

        Assert.Equal(0, legacy.FromPersistence().GetDomain(DomainKind.Forecast).ConsecutiveFailures);
    }

    // --- Stopping and resuming ---

    [Fact]
    public void Stopping_deactivates_and_a_new_start_resumes()
    {
        var state = WithChanges();
        var stopped = state.Apply(new PollingStopped(T0.AddHours(7)));
        var resumed = stopped.Apply(new PollingStarted("lucerne", [(int)DomainKind.Forecast],
            IntervalsFor((DomainKind.Forecast, _forecastInterval)), T0.AddHours(8)));

        Assert.Multiple(
            () => Assert.False(stopped.Active),
            () => Assert.True(resumed.Active),
            () => Assert.Equal(PollPhase.Active, resumed.GetDomain(DomainKind.Forecast).Phase));
    }
}
