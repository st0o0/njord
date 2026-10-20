using Njord.Forecast;
using Njord.Messages.Pipeline;
using Njord.Persistence.Events.Polling;
using Njord.Tests.Shared;

namespace Njord.Pipeline.Tests;

public sealed class PollWorkerEventMappingSpec
{
    private static readonly DateTimeOffset T0 = TestTimeConstants.PipelineEpoch;
    private static readonly TimeSpan DiscoveryInterval = TimeSpan.FromMinutes(20);
    private static readonly int[] WeatherDomain = [(int)DomainKind.Weather];

    private static PollWorkerState Started(DateTimeOffset? at = null, TimeSpan? interval = null) =>
        PollWorkerState.Empty.Apply(new PollingStarted("lucerne", WeatherDomain, interval ?? DiscoveryInterval, at ?? T0));

    private static DomainPolled Changed(int hash, DateTimeOffset at) =>
        new((int)DomainKind.Weather, PersistedDomainPollOutcome.Changed, hash, at, null);

    private static DomainPolled Unchanged(DateTimeOffset at) =>
        new((int)DomainKind.Weather, PersistedDomainPollOutcome.Unchanged, null, at, null);

    private static DomainPolled Failed(DateTimeOffset at, TimeSpan delay) =>
        new((int)DomainKind.Weather, PersistedDomainPollOutcome.Failed, null, at, delay);

    [Fact]
    public void A_changed_event_updates_the_hash_and_last_changed()
    {
        var state = Started().Apply(Changed(42, T0.AddMinutes(20)));
        var weather = state.GetDomain(DomainKind.Weather);

        Assert.Multiple(
            () => Assert.Equal(42, weather.LastHash),
            () => Assert.Equal(T0.AddMinutes(20), weather.LastChanged));
    }

    [Fact]
    public void An_unchanged_event_increments_the_miss_count()
    {
        var state = Started().Apply(Changed(42, T0)).Apply(Unchanged(T0.AddMinutes(20)));
        var weather = state.GetDomain(DomainKind.Weather);

        Assert.Multiple(
            () => Assert.Equal(1, weather.MissCount),
            () => Assert.Equal(T0.AddMinutes(40), weather.NextPollAt));
    }

    [Fact]
    public void A_failed_event_schedules_a_retry_at_the_given_delay()
    {
        var state = Started().Apply(Failed(T0, TimeSpan.FromMinutes(5)));
        var weather = state.GetDomain(DomainKind.Weather);

        Assert.Equal(T0.AddMinutes(5), weather.NextPollAt);
    }

    [Fact]
    public void A_rate_limit_delay_sets_the_retry()
    {
        var state = Started().Apply(Failed(T0, PollWorkerState.RateLimitMinDelay));

        Assert.Equal(T0 + TimeSpan.FromMinutes(5), state.GetDomain(DomainKind.Weather).NextPollAt);
    }

    [Fact]
    public void A_pipeline_unavailable_delay_retries_soon()
    {
        var state = Started().Apply(Failed(T0, PollWorkerState.PipelineUnavailableRetry));

        Assert.Equal(T0 + TimeSpan.FromSeconds(5), state.GetDomain(DomainKind.Weather).NextPollAt);
    }

    [Fact]
    public void An_unknown_outcome_throws()
    {
        var evt = new DomainPolled((int)DomainKind.Weather, (PersistedDomainPollOutcome)99, null, T0, null);

        Assert.Throws<ArgumentOutOfRangeException>(() => Started().Apply(evt));
    }
}
