using Njord.Core;
using Njord.Messages.Polling;
using Njord.Persistence.Events.Polling;
using Njord.Tests.Shared;
using static Njord.Polling.Tests.PollStateFixture;

namespace Njord.Polling.Tests;

public sealed class PollWorkerStateTests
{
    // --- Initial state ---

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
        var weather = state.GetDomain(DomainKind.Forecast);

        Assert.Multiple(
            () => Assert.Equal(new PollTarget(Lucerne), state.Target),
            () => Assert.True(state.Active),
            () => Assert.Equal(Discovery, state.DiscoveryInterval),
            () => Assert.Equal(PollPhase.Discovery, weather.Phase),
            () => Assert.Equal(T0, state.NextPollUtc),
            () => Assert.Null(weather.LastHash));
    }

    // --- Discovery -> Steady progression ---

    [Fact]
    public void First_data_change_stays_in_discovery()
    {
        var state = Started().Apply(Changed(DomainKind.Forecast, 42, T0.AddMinutes(20)));
        var weather = state.GetDomain(DomainKind.Forecast);

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
            .Apply(Changed(DomainKind.Forecast, 42, T0.AddHours(1)))
            .Apply(Changed(DomainKind.Forecast, 99, T0.AddHours(4)));
        var weather = state.GetDomain(DomainKind.Forecast);

        Assert.Multiple(
            () => Assert.Equal(PollPhase.Discovery, weather.Phase),
            () => Assert.Equal(TimeSpan.FromHours(3), weather.CandidateCycle),
            () => Assert.Null(weather.LearnedCycle));
    }

    [Fact]
    public void Third_consistent_change_computes_cycle_and_transitions_to_steady()
    {
        var weather = Steady().GetDomain(DomainKind.Forecast);

        Assert.Multiple(
            () => Assert.Equal(PollPhase.Steady, weather.Phase),
            () => Assert.Equal(TimeSpan.FromHours(3), weather.LearnedCycle),
            () => Assert.Equal(T0.AddHours(6) + TimeSpan.FromHours(3) - DomainPollStateExtensions.SteadyBuffer,
                weather.NextPollAt));
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
    public void An_unchanged_event_increments_the_miss_count()
    {
        var state = Started()
            .Apply(Changed(DomainKind.Forecast, 42, T0))
            .Apply(Unchanged(DomainKind.Forecast, T0.AddMinutes(20)));
        var weather = state.GetDomain(DomainKind.Forecast);

        Assert.Multiple(
            () => Assert.Equal(1, weather.MissCount),
            () => Assert.Equal(T0.AddMinutes(40), weather.NextPollAt));
    }

    [Fact]
    public void Data_change_resets_miss_count()
    {
        var state = Steady();
        var now = T0.AddHours(9);
        for (var i = 0; i < 3; i++)
        {
            state = state.Apply(Unchanged(DomainKind.Forecast, now));
            var next = state.GetDomain(DomainKind.Forecast).NextPollAt;
            Assert.NotNull(next);
            now = next.Value;
        }

        Assert.Equal(3, state.GetDomain(DomainKind.Forecast).MissCount);

        state = state.Apply(Changed(DomainKind.Forecast, 4, now));
        Assert.Equal(0, state.GetDomain(DomainKind.Forecast).MissCount);
    }

    // --- Failure events ---

    [Fact]
    public void A_failed_event_schedules_a_retry_at_the_given_delay()
    {
        var state = Started().Apply(Failed(DomainKind.Forecast, T0, TimeSpan.FromMinutes(5)));

        Assert.Equal(T0.AddMinutes(5), state.GetDomain(DomainKind.Forecast).NextPollAt);
    }

    [Fact]
    public void A_rate_limit_delay_sets_the_retry()
    {
        var state = Started().Apply(Failed(DomainKind.Forecast, T0, PollWorkerState.RateLimitMinDelay));

        Assert.Equal(T0 + TimeSpan.FromMinutes(5), state.GetDomain(DomainKind.Forecast).NextPollAt);
    }

    [Fact]
    public void A_pipeline_unavailable_delay_retries_soon()
    {
        var state = Started().Apply(Failed(DomainKind.Forecast, T0, PollWorkerState.PipelineUnavailableRetry));

        Assert.Equal(T0 + TimeSpan.FromSeconds(5), state.GetDomain(DomainKind.Forecast).NextPollAt);
    }

    [Fact]
    public void A_failed_event_records_the_consecutive_failure_count()
    {
        var evt = Failed(DomainKind.Forecast, T0, TimeSpan.FromMinutes(2)) with { ConsecutiveFailures = 2 };

        Assert.Equal(2, Started().Apply(evt).GetDomain(DomainKind.Forecast).ConsecutiveFailures);
    }

    [Fact]
    public void A_changed_event_resets_the_consecutive_failure_count()
    {
        var state = Started()
            .Apply(Failed(DomainKind.Forecast, T0, TimeSpan.FromMinutes(2)) with { ConsecutiveFailures = 2 })
            .Apply(Changed(DomainKind.Forecast, 42, T0.AddMinutes(2)));

        Assert.Equal(0, state.GetDomain(DomainKind.Forecast).ConsecutiveFailures);
    }

    [Fact]
    public void Failures_preserve_phase_and_cycle()
    {
        var state = Steady();
        var now = T0.AddHours(9);
        for (var i = 0; i < 20; i++)
        {
            state = state.Apply(Failed(DomainKind.Forecast, now));
            var next = state.GetDomain(DomainKind.Forecast).NextPollAt;
            Assert.NotNull(next);
            now = next.Value;
        }

        var weather = state.GetDomain(DomainKind.Forecast);
        Assert.Multiple(
            () => Assert.Equal(PollPhase.Steady, weather.Phase),
            () => Assert.Equal(TimeSpan.FromHours(3), weather.LearnedCycle));
    }

    [Fact]
    public void An_unknown_outcome_throws()
    {
        var evt = new DomainPolled((int)DomainKind.Forecast, (PersistedDomainPollOutcome)99, null, T0, null);

        Assert.Throws<ArgumentOutOfRangeException>(() => Started().Apply(evt));
    }

    // --- Miss backoff and fallback ---

    [Fact]
    public void Retry_backoff_doubles_per_miss()
    {
        var state = Steady();
        var now = T0.AddHours(9);
        TimeSpan[] expected = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(8)];

        foreach (var delay in expected)
        {
            state = state.Apply(Unchanged(DomainKind.Forecast, now));
            var next = state.GetDomain(DomainKind.Forecast).NextPollAt;
            Assert.NotNull(next);
            Assert.Equal(now + delay, next);
            now = next.Value;
        }

        Assert.Equal(4, state.GetDomain(DomainKind.Forecast).MissCount);
    }

    [Fact]
    public void Retry_backoff_never_exceeds_15_minutes()
    {
        var state = Steady();
        var now = T0.AddHours(9);
        for (var i = 0; i < 4; i++)
        {
            state = state.Apply(Unchanged(DomainKind.Forecast, now));
            var next = state.GetDomain(DomainKind.Forecast).NextPollAt;
            Assert.NotNull(next);
            var nextAt = next.Value;
            Assert.True(nextAt - now <= TimeSpan.FromMinutes(15), $"miss {i + 1} waits {nextAt - now}");
            now = nextAt;
        }

        Assert.Equal(PollPhase.Steady, state.GetDomain(DomainKind.Forecast).Phase);
    }

    [Fact]
    public void Five_misses_in_steady_falls_back_to_discovery()
    {
        var state = Steady();
        Assert.Equal(PollPhase.Steady, state.GetDomain(DomainKind.Forecast).Phase);

        var now = T0.AddHours(9);
        var lastMiss = now;
        for (var i = 0; i < 5; i++)
        {
            lastMiss = now;
            state = state.Apply(Unchanged(DomainKind.Forecast, now));
            var next = state.GetDomain(DomainKind.Forecast).NextPollAt;
            Assert.NotNull(next);
            now = next.Value;
        }

        var weather = state.GetDomain(DomainKind.Forecast);
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
            state = state.Apply(Unchanged(DomainKind.Forecast, now));
            var weather = state.GetDomain(DomainKind.Forecast);
            Assert.Equal(PollPhase.Discovery, weather.Phase);
            Assert.Equal(now + Discovery, weather.NextPollAt);
            Assert.NotNull(weather.NextPollAt);
            now = weather.NextPollAt.Value;
        }

        Assert.Equal(10, state.GetDomain(DomainKind.Forecast).MissCount);
    }

    // --- Replan (StartPolling with changed interval) ---

    [Fact]
    public void A_shorter_discovery_interval_replans_a_pending_discovery_poll()
    {
        var state = Started().Apply(Changed(DomainKind.Forecast, 42, T0));

        var replanned = state.Apply(new PollingStarted("lucerne", [(int)DomainKind.Forecast], TimeSpan.FromMinutes(5), T0.AddMinutes(1)));

        Assert.Multiple(
            () => Assert.Equal(T0.AddMinutes(5), replanned.GetDomain(DomainKind.Forecast).NextPollAt),
            () => Assert.Equal(TimeSpan.FromMinutes(5), replanned.DiscoveryInterval));
    }

    [Fact]
    public void A_replanned_poll_is_never_earlier_than_now()
    {
        var state = Started().Apply(Changed(DomainKind.Forecast, 42, T0));

        var replanned = state.Apply(new PollingStarted("lucerne", [(int)DomainKind.Forecast], TimeSpan.FromMinutes(5), T0.AddMinutes(10)));

        Assert.Equal(T0.AddMinutes(10), replanned.GetDomain(DomainKind.Forecast).NextPollAt);
    }

    [Fact]
    public void A_longer_discovery_interval_moves_the_next_poll_out()
    {
        var state = Started().Apply(Changed(DomainKind.Forecast, 42, T0));

        var replanned = state.Apply(new PollingStarted("lucerne", [(int)DomainKind.Forecast], TimeSpan.FromHours(1), T0));

        Assert.Equal(T0.AddHours(1), replanned.GetDomain(DomainKind.Forecast).NextPollAt);
    }

    [Fact]
    public void Steady_delays_are_not_replanned()
    {
        var steady = Steady();
        var weatherBefore = steady.GetDomain(DomainKind.Forecast).NextPollAt;

        var replanned = steady.Apply(new PollingStarted("lucerne", [(int)DomainKind.Forecast], TimeSpan.FromHours(2), T0.AddHours(6)));

        Assert.Equal(weatherBefore, replanned.GetDomain(DomainKind.Forecast).NextPollAt);
    }

    [Fact]
    public void A_start_with_the_same_interval_keeps_the_state()
    {
        var state = Steady();

        var restarted = state.Apply(new PollingStarted("lucerne", [(int)DomainKind.Forecast], Discovery, T0.AddHours(7)));

        Assert.Equal(state.Domains, restarted.Domains);
    }

    [Fact]
    public void Stopping_deactivates_and_a_new_start_resumes_the_learned_state()
    {
        var stopped = Steady().Apply(new PollingStopped(T0.AddHours(7)));
        var resumed = stopped.Apply(new PollingStarted("lucerne", [(int)DomainKind.Forecast], Discovery, T0.AddHours(8)));

        Assert.Multiple(
            () => Assert.False(stopped.Active),
            () => Assert.True(resumed.Active),
            () => Assert.Equal(TimeSpan.FromHours(3), resumed.GetDomain(DomainKind.Forecast).LearnedCycle),
            () => Assert.Equal(Steady().GetDomain(DomainKind.Forecast).NextPollAt, resumed.GetDomain(DomainKind.Forecast).NextPollAt));
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
        var legacy = new PersistedPollWorkerState("lucerne", true, Discovery,
            [new PersistedDomainState((int)DomainKind.Forecast, 7)]);

        Assert.Equal(0, legacy.FromPersistence().GetDomain(DomainKind.Forecast).ConsecutiveFailures);
    }
}
