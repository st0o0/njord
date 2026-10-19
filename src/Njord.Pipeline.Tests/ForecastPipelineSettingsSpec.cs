using Akka.Actor;
using Microsoft.Extensions.Time.Testing;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Alerts.Configuration;
using Njord.Core.Actors;
using Njord.Core.Domain;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Pipeline.Tests;

public sealed class ForecastPipelineSettingsSpec : NjordActorSpec
{
    private static readonly DateTimeOffset Start = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    protected override bool UseTestPersistence => false;

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Default_thresholds_raise_no_frost_alert_for_mild_weather()
    {
        var ct = TestContext.Current.CancellationToken;
        var (queue, alerts) = await StartAsync(ct);

        await queue.OfferAsync(DomainData.Received(DomainData.Lucerne, DomainData.Weather(Start)), ct);

        await alerts.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(500), ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Constructor_injected_thresholds_decide_the_alert_severity()
    {
        var ct = TestContext.Current.CancellationToken;
        // The mild forecast is far below these thresholds; the default thresholds (0, -5, -15) never alert.
        var (queue, alerts) = await StartAsync(ct, o => o.FrostThresholds = [60, 55, 50]);

        await queue.OfferAsync(DomainData.Received(DomainData.Lucerne, DomainData.Weather(Start)), ct);

        var frost = await alerts.FishForMessageAsync(
            m => m is PipelineEvent.AlertRaised { Type: AlertType.Frost }, TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        var raised = Assert.IsType<PipelineEvent.AlertRaised>(frost);
        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Red, raised.Severity),
            () => Assert.Equal(10.0, raised.Alert.TriggerValue),
            () => Assert.Equal(50.0, raised.Alert.Threshold));
    }

    private async Task<(ISourceQueueWithComplete<DomainDataReceived> Queue, Akka.TestKit.TestProbe Alerts)> StartAsync(
        CancellationToken ct, Action<AlertOptions>? configure = null)
    {
        var time = new FakeTimeProvider(Start);
        var group = FeatureServices.AlertGroup(time, configure);
        var pipeline = Sys.ActorOf(Props.Create(() => new ForecastPipeline(time, new[] { group })), $"pipeline-{Guid.NewGuid():N}");

        var output = await pipeline.Ask<RequestPipelineOutputResponse>(new RequestPipelineOutput(1), TimeSpan.FromSeconds(9), ct);
        var completed = Assert.IsType<RequestPipelineOutputCompleted>(output);
        var probe = CreateTestProbe();
        completed.SourceRef.Source
            .Where(e => e is PipelineEvent.AlertRaised)
            .RunWith(Sink.ActorRef<PipelineEvent>(probe.Ref, "done", ex => ex), Sys.Materializer());
        var queue = await PipelineProducer.AttachAsync(pipeline, Sys, ct);
        return (queue, probe);
    }
}
