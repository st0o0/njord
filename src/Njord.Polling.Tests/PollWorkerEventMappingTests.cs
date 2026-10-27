using Njord.Forecast;
using Njord.Persistence.Events.Polling;
using Njord.Tests.Shared;

namespace Njord.Polling.Tests;

public sealed class PollWorkerEventMappingTests
{
    private static readonly DateTimeOffset T0 = TestTimeConstants.PipelineEpoch;
    private static readonly TimeSpan DiscoveryInterval = TimeSpan.FromMinutes(20);
    private static readonly int[] ForecastDomain = [(int)DomainKind.Forecast];

    private static PollWorkerState Started(DateTimeOffset? at = null, TimeSpan? interval = null) =>
        PollWorkerState.Empty.Apply(new PollingStarted("lucerne", ForecastDomain, interval ?? DiscoveryInterval, at ?? T0));

    private static DomainPolled Changed(int hash, DateTimeOffset at) =>
        new((int)DomainKind.Forecast, PersistedDomainPollOutcome.Changed, hash, at, null);

    private static DomainPolled Unchanged(DateTimeOffset at) =>
        new((int)DomainKind.Forecast, PersistedDomainPollOutcome.Unchanged, null, at, null);

    private static DomainPolled Failed(DateTimeOffset at, TimeSpan delay) =>
        new((int)DomainKind.Forecast, PersistedDomainPollOutcome.Failed, null, at, delay);

    [Fact]
    public void A_changed_event_updates_the_hash_and_last_changed()
    {
        var state = Started().Apply(Changed(42, T0.AddMinutes(20)));
        var weather = state.GetDomain(DomainKind.Forecast);

        Assert.Multiple(
            () => Assert.Equal(42, weather.LastHash),
            () => Assert.Equal(T0.AddMinutes(20), weather.LastChanged));
    }

    [Fact]
    public void An_unchanged_event_increments_the_miss_count()
    {
        var state = Started().Apply(Changed(42, T0)).Apply(Unchanged(T0.AddMinutes(20)));
        var weather = state.GetDomain(DomainKind.Forecast);

        Assert.Multiple(
            () => Assert.Equal(1, weather.MissCount),
            () => Assert.Equal(T0.AddMinutes(40), weather.NextPollAt));
    }

    [Fact]
    public void A_failed_event_schedules_a_retry_at_the_given_delay()
    {
        var state = Started().Apply(Failed(T0, TimeSpan.FromMinutes(5)));
        var weather = state.GetDomain(DomainKind.Forecast);

        Assert.Equal(T0.AddMinutes(5), weather.NextPollAt);
    }

    [Fact]
    public void A_rate_limit_delay_sets_the_retry()
    {
        var state = Started().Apply(Failed(T0, PollWorkerState.RateLimitMinDelay));

        Assert.Equal(T0 + TimeSpan.FromMinutes(5), state.GetDomain(DomainKind.Forecast).NextPollAt);
    }

    [Fact]
    public void A_pipeline_unavailable_delay_retries_soon()
    {
        var state = Started().Apply(Failed(T0, PollWorkerState.PipelineUnavailableRetry));

        Assert.Equal(T0 + TimeSpan.FromSeconds(5), state.GetDomain(DomainKind.Forecast).NextPollAt);
    }

    [Fact]
    public void A_failed_event_records_the_consecutive_failure_count()
    {
        var evt = Failed(T0, TimeSpan.FromMinutes(2)) with { ConsecutiveFailures = 2 };

        Assert.Equal(2, Started().Apply(evt).GetDomain(DomainKind.Forecast).ConsecutiveFailures);
    }

    [Fact]
    public void A_changed_event_resets_the_consecutive_failure_count()
    {
        var state = Started().Apply(Failed(T0, TimeSpan.FromMinutes(2)) with { ConsecutiveFailures = 2 })
            .Apply(Changed(42, T0.AddMinutes(2)));

        Assert.Equal(0, state.GetDomain(DomainKind.Forecast).ConsecutiveFailures);
    }

    [Fact]
    public void The_consecutive_failure_count_survives_a_snapshot_round_trip()
    {
        var state = Started().Apply(Failed(T0, TimeSpan.FromMinutes(4)) with { ConsecutiveFailures = 3 });

        var restored = state.GetPersistenceState().FromPersistence();

        Assert.Equal(3, restored.GetDomain(DomainKind.Forecast).ConsecutiveFailures);
    }

    [Fact]
    public void A_persisted_domain_without_a_failure_count_recovers_as_zero()
    {
        var legacy = new PersistedPollWorkerState("lucerne", true, DiscoveryInterval,
            [new PersistedDomainState((int)DomainKind.Forecast, 7)]);

        Assert.Equal(0, legacy.FromPersistence().GetDomain(DomainKind.Forecast).ConsecutiveFailures);
    }

    [Fact]
    public void An_unknown_outcome_throws()
    {
        var evt = new DomainPolled((int)DomainKind.Forecast, (PersistedDomainPollOutcome)99, null, T0, null);

        Assert.Throws<ArgumentOutOfRangeException>(() => Started().Apply(evt));
    }
}
