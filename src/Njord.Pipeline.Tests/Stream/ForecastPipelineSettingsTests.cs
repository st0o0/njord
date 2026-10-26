using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core.Actors;
using Njord.Core.Domain;
using Njord.Features.Alerts.Frost;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Pipeline.Tests;

public sealed class ForecastPipelineSettingsTests : Akka.Hosting.TestKit.TestKit
{
    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider) { }

    private static readonly DateTimeOffset Start = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact(Timeout = 30000)]
    public async Task Default_thresholds_raise_no_frost_alert_for_mild_weather()
    {
        var ct = TestContext.Current.CancellationToken;
        var (queue, alerts) = await StartAsync(ct);

        await queue.OfferAsync(DomainData.Received(DomainData.Lucerne, DomainData.Weather(Start)), ct);

        await alerts.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(500), ct);
    }

    [Fact(Timeout = 30000)]
    public async Task Constructor_injected_thresholds_decide_the_alert_severity()
    {
        var ct = TestContext.Current.CancellationToken;
        // The mild forecast is far below these thresholds; the default thresholds (0, -5, -15) never alert.
        var (queue, alerts) = await StartAsync(ct, new FrostConfig { Thresholds = [60, 55, 50] });

        await queue.OfferAsync(DomainData.Received(DomainData.Lucerne, DomainData.Weather(Start)), ct);

        var frost = await alerts.FishForMessageAsync(
            m => m is FrostAlertResult, TimeSpan.FromSeconds(10), cancellationToken: ct);
        var raised = Assert.IsType<FrostAlertResult>(frost);
        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Red, raised.Severity),
            () => Assert.Equal(10.0, raised.TriggerValue),
            () => Assert.Equal(50.0, raised.Threshold));
    }

    private async Task<(ISourceQueueWithComplete<DomainDataReceived> Queue, Akka.TestKit.TestProbe Alerts)> StartAsync(
        CancellationToken ct, FrostConfig? frost = null)
    {
        var time = new FakeTimeProvider(Start);
        var rule = new FrostAlert(Options.Create(frost ?? new FrostConfig()));
        var pipeline = Sys.ActorOf(Props.Create(() => new ForecastPipeline(time, new IFeatureRule[] { rule })), $"pipeline-{Guid.NewGuid():N}");

        var output = await pipeline.Ask<RequestPipelineOutputResponse>(new RequestPipelineOutput(1), TimeSpan.FromSeconds(9), ct);
        var completed = Assert.IsType<RequestPipelineOutputCompleted>(output);
        var probe = CreateTestProbe();
        completed.SourceRef.Source
            .Where(e => e is AlertFeatureResult)
            .RunWith(Sink.ActorRef<PipelineEvent>(probe.Ref, "done", ex => ex), Sys.Materializer());
        var queue = await PipelineProducer.AttachAsync(pipeline, Sys, ct);
        return (queue, probe);
    }
}
