using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Compute.Contracts;
using Njord.Core.Actors;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;
using Njord.Weather;
using WeatherForecast = Njord.Weather.Forecast;

namespace Njord.Pipeline.Tests;

public sealed class ForecastReadModelSpec : NjordActorSpec
{
    private static readonly DateTimeOffset Anchor = TestTimeConstants.NoonAnchor;
    private static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(3);
    private static readonly LocationId Lucerne = new("lucerne");

    protected override bool UseTestPersistence => false;

    private async Task<(IActorRef ReadModel, ISourceQueueWithComplete<PipelineEvent> Pipeline)> StartAsync()
    {
        var ready = new TaskCompletionSource<ISourceQueueWithComplete<PipelineEvent>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var pipeline = Sys.ActorOf(Props.Create(() => new FakePipeline(Sys.Materializer(), ready)),
            $"pipeline-{Guid.NewGuid():N}");
        ActorRegistry.Register<IForecastPipeline>(pipeline, overwrite: true);

        var readModel = Sys.ActorOf(Props.Create(() => new ForecastReadModel()), $"read-model-{Guid.NewGuid():N}");
        var queue = await ready.Task.WaitAsync(TestTimeouts.AwaitAssertMax, TestContext.Current.CancellationToken);
        return (readModel, queue);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_forecast_query_returns_the_latest_forecast_for_the_location_and_model()
    {
        var ct = TestContext.Current.CancellationToken;
        var (readModel, pipeline) = await StartAsync();
        var forecast = CreateForecast("icon_d2");

        await pipeline.OfferAsync(new PipelineEvent.ForecastReceived(Lucerne, forecast.Model, forecast), ct);

        await AwaitAssertAsync(async () =>
        {
            var response = await readModel.Ask<ForecastResponse>(new QueryForecast(Lucerne, "ICON_D2"), AskTimeout, ct);
            var result = Assert.IsType<ForecastResult>(response);
            Assert.Same(forecast, result.Forecast);
        }, TestTimeouts.AwaitAssertMax, cancellationToken: ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_latest_analysis_query_returns_the_last_analysis_event_of_the_location()
    {
        var ct = TestContext.Current.CancellationToken;
        var (readModel, pipeline) = await StartAsync();
        var first = new PipelineEvent.AlertComputed(new AlertResult(Lucerne, []), Anchor);
        var last = new PipelineEvent.AlertComputed(new AlertResult(Lucerne, []), Anchor.AddHours(1));

        await pipeline.OfferAsync(first, ct);
        await pipeline.OfferAsync(last, ct);

        await AwaitAssertAsync(async () =>
        {
            var response = await readModel.Ask<LatestAnalysisResponse>(new QueryLatestAnalysis(Lucerne), AskTimeout, ct);
            var result = Assert.IsType<LatestAnalysisResult>(response);
            Assert.Equal(last, result.Analysis);
        }, TestTimeouts.AwaitAssertMax, cancellationToken: ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Queries_before_the_first_cycle_report_that_no_data_is_available()
    {
        var ct = TestContext.Current.CancellationToken;
        var (readModel, _) = await StartAsync();

        var forecast = await readModel.Ask<ForecastResponse>(new QueryForecast(Lucerne, "icon_d2"), AskTimeout, ct);
        var analysis = await readModel.Ask<LatestAnalysisResponse>(new QueryLatestAnalysis(Lucerne), AskTimeout, ct);

        Assert.Equal(new ForecastNotAvailable("lucerne|icon_d2"), forecast);
        Assert.Equal(new LatestAnalysisNotAvailable(Lucerne), analysis);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_streaming_client_receives_pipeline_events_from_the_secondary_hub()
    {
        var ct = TestContext.Current.CancellationToken;
        var (readModel, pipeline) = await StartAsync();
        var probe = CreateTestProbe();

        var source = await RequestStreamAsync(readModel, ct);
        source.RunWith(Sink.ActorRef<PipelineEvent>(probe.Ref, "done", _ => "failed"), Sys.Materializer());
        var alert = new PipelineEvent.AlertComputed(new AlertResult(Lucerne, []), Anchor);

        await AwaitAssertAsync(async () =>
        {
            await pipeline.OfferAsync(alert, ct);
            await probe.ExpectMsgAsync<PipelineEvent.AlertComputed>(TimeSpan.FromMilliseconds(500), cancellationToken: ct);
        }, TestTimeouts.AwaitAssertMax, cancellationToken: ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_disconnected_streaming_client_does_not_affect_other_clients()
    {
        var ct = TestContext.Current.CancellationToken;
        var (readModel, pipeline) = await StartAsync();
        var mat = Sys.Materializer();
        var probe = CreateTestProbe();

        var dead = await RequestStreamAsync(readModel, ct);
        var killSwitch = dead.ViaMaterialized(KillSwitches.Single<PipelineEvent>(), Keep.Right)
            .To(Sink.Ignore<PipelineEvent>())
            .Run(mat);
        killSwitch.Shutdown();

        var alive = await RequestStreamAsync(readModel, ct);
        alive.RunWith(Sink.ActorRef<PipelineEvent>(probe.Ref, "done", _ => "failed"), mat);
        var alert = new PipelineEvent.AlertComputed(new AlertResult(Lucerne, []), Anchor);

        await AwaitAssertAsync(async () =>
        {
            await pipeline.OfferAsync(alert, ct);
            await probe.ExpectMsgAsync<PipelineEvent.AlertComputed>(TimeSpan.FromMilliseconds(500), cancellationToken: ct);
        }, TestTimeouts.AwaitAssertMax, cancellationToken: ct);
    }

    private static async Task<Source<PipelineEvent, Akka.NotUsed>> RequestStreamAsync(IActorRef readModel, CancellationToken ct)
    {
        RequestReadModelStreamResponse? response = null;
        for (var attempt = 0; attempt < 50 && response is not RequestReadModelStreamCompleted; attempt++)
        {
            response = await readModel.Ask<RequestReadModelStreamResponse>(new RequestReadModelStream(attempt), AskTimeout, ct);
            if (response is RequestReadModelStreamFailed)
            {
                await Task.Delay(100, ct);
            }
        }

        var completed = Assert.IsType<RequestReadModelStreamCompleted>(response);
        return completed.SourceRef.Source;
    }

    private static WeatherForecast CreateForecast(string model)
    {
        var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
        var points = new List<ForecastPoint>
        {
            new(Anchor.AddHours(3), new Dictionary<ParameterDef, double?> { [temp] = 28.8 }),
        };
        return new WeatherForecast(new WeatherModel(model), "lucerne", new CycleId(Anchor),
            new ForecastSeries(points), DailyForecastSeries.Empty);
    }

    private sealed class FakePipeline : ReceiveActor
    {
        public FakePipeline(IMaterializer mat, TaskCompletionSource<ISourceQueueWithComplete<PipelineEvent>> ready)
        {
            Receive<RequestPipelineOutput>(msg =>
            {
                var (queue, source) = Source.Queue<PipelineEvent>(32, OverflowStrategy.DropHead).PreMaterialize(mat);
                ready.TrySetResult(queue);
                source
                    .RunWith(StreamRefs.SourceRef<PipelineEvent>(), mat)
                    .PipeTo(Sender, Self,
                        sr => new RequestPipelineOutputCompleted(msg.RequestId, sr),
                        ex => new RequestPipelineOutputFailed(msg.RequestId, ex));
            });
        }
    }
}
