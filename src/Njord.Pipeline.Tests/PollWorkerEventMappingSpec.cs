using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Persistence.Events.Polling;
using Njord.Tests.Shared;

namespace Njord.Pipeline.Tests;

public sealed class PollWorkerEventMappingSpec
{
    private static readonly DateTimeOffset T0 = TestTimeConstants.PipelineEpoch;
    private static readonly TimeSpan DiscoveryInterval = TimeSpan.FromMinutes(20);
    private static readonly Location Lucerne = new("lucerne", 0, 0);

    private static PollWorkerState Started(DateTimeOffset? at = null, TimeSpan? interval = null) =>
        PollWorkerState.Empty.Apply(new PollingStarted("lucerne", interval ?? DiscoveryInterval, at ?? T0));

    // Two changes three hours apart: Steady with a 3 h cycle.
    private static PollWorkerState Steady() =>
        Started().Apply(new ForecastChanged(1, T0)).Apply(new ForecastChanged(2, T0.AddHours(3)));

    [Fact]
    public void A_changed_hash_becomes_a_forecast_changed_event()
    {
        var state = Started().Apply(new ForecastChanged(42, T0)).WithDomainHash(DomainKind.Weather, 42);

        var (evt, outcome) = state.ToEvent(new FetchDomainCompleted(Lucerne, DomainKind.Weather, 99), T0.AddHours(1));

        Assert.Multiple(
            () => Assert.Equal(new ForecastChanged(99, T0.AddHours(1)), evt),
            () => Assert.Equal(PollWorkerOutcome.Changed, outcome));
    }

    [Fact]
    public void The_first_hash_is_a_change()
    {
        var (evt, outcome) = Started().ToEvent(new FetchDomainCompleted(Lucerne, DomainKind.Weather, 7), T0);

        Assert.Multiple(
            () => Assert.Equal(new ForecastChanged(7, T0), evt),
            () => Assert.Equal(PollWorkerOutcome.Changed, outcome));
    }

    [Fact]
    public void The_same_hash_becomes_a_forecast_unchanged_event()
    {
        var state = Started().Apply(new ForecastChanged(42, T0)).WithDomainHash(DomainKind.Weather, 42);

        var (evt, outcome) = state.ToEvent(new FetchDomainCompleted(Lucerne, DomainKind.Weather, 42), T0.AddHours(1));

        Assert.Multiple(
            () => Assert.Equal(new ForecastUnchanged(T0.AddHours(1)), evt),
            () => Assert.Equal(PollWorkerOutcome.Unchanged, outcome));
    }

    [Theory]
    [InlineData(PollFailureReason.Transport, 0)]
    [InlineData(PollFailureReason.ModelUnavailable, 0)]
    [InlineData(PollFailureReason.MalformedPayload, 0)]
    [InlineData(PollFailureReason.RateLimited, 5)]
    public void A_failure_becomes_a_poll_failed_event_with_its_minimum_delay(PollFailureReason reason, int minimumMinutes)
    {
        var (evt, outcome) = Started().ToEvent(new FetchDomainFailed(Lucerne, DomainKind.Weather, reason, "x"), T0);

        Assert.Multiple(
            () => Assert.Equal(new PollFailed(T0, TimeSpan.FromMinutes(minimumMinutes)), evt),
            () => Assert.Equal(PollWorkerOutcome.Failed, outcome));
    }

    [Theory]
    [InlineData(FetchRejectionReason.Budget, 5)]
    [InlineData(FetchRejectionReason.QueueFull, 0)]
    [InlineData(FetchRejectionReason.Expired, 0)]
    public void A_rejection_becomes_a_poll_failed_event_with_its_minimum_delay(FetchRejectionReason reason, int minimumMinutes)
    {
        var (evt, outcome) = Started().ToEvent(new FetchDomainRejected(Lucerne, DomainKind.Weather, reason), T0);

        Assert.Multiple(
            () => Assert.Equal(new PollFailed(T0, TimeSpan.FromMinutes(minimumMinutes)), evt),
            () => Assert.Equal(PollWorkerOutcome.Failed, outcome));
    }

    [Fact]
    public void A_pipeline_unavailable_rejection_becomes_a_deferral()
    {
        var (evt, outcome) = Started().ToEvent(
            new FetchDomainRejected(Lucerne, DomainKind.Weather, FetchRejectionReason.PipelineUnavailable), T0);

        Assert.Multiple(
            () => Assert.Equal(new PollDeferred(T0, TimeSpan.FromSeconds(5)), evt),
            () => Assert.Equal(PollWorkerOutcome.Failed, outcome));
    }
}
