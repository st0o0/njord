using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Alerts;
using Njord.Compute.Analysis;
using Njord.Compute.Settings;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Alerts;
using Njord.Core.Configuration.Consensus;
using Njord.Core.Configuration.Derived;
using Njord.Core.Configuration.Indices;
using Njord.Core.Configuration.Trends;
using Njord.Core.Egress;
using Njord.Messages.Forecast;
using Njord.Tests.Shared;
using Njord.Weather;
using WeatherForecast = Njord.Weather.Forecast;

namespace Njord.Pipeline.Tests;

public sealed class ForecastPipelineSettingsSpec : NjordActorSpec
{
    protected override bool UseTestPersistence => false;

    private static AnalysisSettings SettingsWith(AlertOptions alerts) =>
        AnalysisSettingsFactory.Create(
            new NjordOptions { Locations = [new LocationOptions { Name = AnalysisFixture.Lucerne }] },
            new ConsensusOptions(), alerts, new IndexOptions(), new TrendOptions(), new DerivedOptions());

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_settings_change_applies_from_the_next_forecast_on()
    {
        var ct = TestContext.Current.CancellationToken;
        var fixture = AnalysisFixture.Create();
        var forecasts = fixture.Forecasts.Where(f => f.Location == AnalysisFixture.Lucerne).ToList();
        var queueReady = new TaskCompletionSource<ISourceQueueWithComplete<WeatherForecast>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        ActorRegistry.Register<IForecastGateway>(
            Sys.ActorOf(Props.Create(() => new FakeGateway(queueReady)), $"gateway-{Guid.NewGuid():N}"),
            overwrite: true);

        var pipeline = Sys.ActorOf(Props.Create(() => new ForecastPipeline(
            fixture.Time,
            new ConsensusSnapshotFactory(AnalysisFixture.Parameters, fixture.Time),
            new AlertAnalyzer(Options.Create(new AlertOptions()), fixture.Time),
            DisabledAnalyzers.Derived(),
            DisabledAnalyzers.Trend(),
            DisabledAnalyzers.Index(),
            SettingsWith(new AlertOptions()))), $"pipeline-{Guid.NewGuid():N}");

        var output = await pipeline.Ask<RequestPipelineOutputResponse>(new RequestPipelineOutput(1), TimeSpan.FromSeconds(9), ct);
        var completed = Assert.IsType<RequestPipelineOutputCompleted>(output);
        var probe = CreateTestProbe();
        completed.SourceRef.Source
            .Collect(e => e as PipelineEvent.AlertComputed)
            .RunWith(Sink.ActorRef<PipelineEvent.AlertComputed>(probe.Ref, "done", ex => ex), Sys.Materializer());
        var queue = await queueReady.Task.WaitAsync(ct);

        await queue.OfferAsync(forecasts[0], ct);
        await queue.OfferAsync(forecasts[1], ct);
        await probe.ExpectMsgAsync<PipelineEvent.AlertComputed>(cancellationToken: ct);
        var beforeChange = await probe.ExpectMsgAsync<PipelineEvent.AlertComputed>(cancellationToken: ct);
        Assert.NotEqual(AlertSeverity.None, FrostSeverity(beforeChange));

        pipeline.Tell(new AnalysisSettingsChanged(SettingsWith(new AlertOptions { FrostThresholds = [-50, -60, -70] })));

        await AwaitAssertAsync(async () =>
        {
            await queue.OfferAsync(forecasts[2], ct);
            var next = await probe.ExpectMsgAsync<PipelineEvent.AlertComputed>(cancellationToken: ct);
            Assert.Equal(AlertSeverity.None, FrostSeverity(next));
        }, TestTimeouts.AwaitAssertMax, cancellationToken: ct);
    }

    private static AlertSeverity FrostSeverity(PipelineEvent.AlertComputed computed) =>
        computed.Result.Alerts.Single(a => a.Type == AlertType.Frost).Severity;

    private sealed class FakeGateway : ReceiveActor
    {
        public FakeGateway(TaskCompletionSource<ISourceQueueWithComplete<WeatherForecast>> queueReady)
        {
            var mat = Context.Materializer();

            Receive<RequestForecastStream>(msg =>
            {
                var (queue, source) = Source.Queue<WeatherForecast>(16, OverflowStrategy.Backpressure).PreMaterialize(mat);
                queueReady.TrySetResult(queue);
                source.RunWith(StreamRefs.SourceRef<WeatherForecast>(), mat)
                    .PipeTo(Sender,
                        success: sourceRef => new RequestForecastStreamCompleted(msg.RequestId, sourceRef),
                        failure: ex => new RequestForecastStreamFailed(msg.RequestId, ex));
            });
        }
    }
}
