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
    private static readonly LocationId Zurich = new("zurich");

    protected override bool UseTestPersistence => false;

    private async Task<(IActorRef ReadModel, ISourceQueueWithComplete<PipelineEvent> Pipeline)> StartAsync()
    {
        var (readModel, pipeline) = await StartWithPipelineHubAsync();
        return (readModel, pipeline.Queue);
    }

    private async Task<(IActorRef ReadModel, TestPipeline Pipeline)> StartWithPipelineHubAsync()
    {
        var ready = new TaskCompletionSource<TestPipeline>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pipeline = Sys.ActorOf(Props.Create(() => new FakePipeline(Sys.Materializer(), ready)),
            $"pipeline-{Guid.NewGuid():N}");
        ActorRegistry.Register<IForecastPipeline>(pipeline, overwrite: true);

        var readModel = Sys.ActorOf(Props.Create(() => new ForecastReadModel()), $"read-model-{Guid.NewGuid():N}");
        var testPipeline = await ready.Task.WaitAsync(TestTimeouts.RealTimeWaitMax, TestContext.Current.CancellationToken);
        return (readModel, testPipeline);
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
    public async Task Queries_match_the_location_regardless_of_case()
    {
        var ct = TestContext.Current.CancellationToken;
        var (readModel, pipeline) = await StartAsync();
        var forecast = CreateForecast("icon_d2");
        var alerts = EnrichmentEvents.Alerts(Lucerne, Anchor);

        await pipeline.OfferAsync(new PipelineEvent.ForecastReceived(Lucerne, forecast.Model, forecast), ct);
        await pipeline.OfferAsync(alerts, ct);

        var shouted = new LocationId("LUCERNE");
        await AwaitAssertAsync(async () =>
        {
            var response = await readModel.Ask<ForecastResponse>(new QueryForecast(shouted, "icon_d2"), AskTimeout, ct);
            Assert.Same(forecast, Assert.IsType<ForecastResult>(response).Forecast);
        }, TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        var analysis = await AwaitAnalysisAsync(readModel, shouted, r => Equals(r.Alerts, alerts), ct);
        Assert.Equal(alerts, analysis.Alerts);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_latest_analysis_query_returns_the_last_event_of_a_kind()
    {
        var ct = TestContext.Current.CancellationToken;
        var (readModel, pipeline) = await StartAsync();
        var first = EnrichmentEvents.Alerts(Lucerne, Anchor);
        var last = EnrichmentEvents.Alerts(Lucerne, Anchor.AddHours(1));

        await pipeline.OfferAsync(first, ct);
        await pipeline.OfferAsync(last, ct);

        var result = await AwaitAnalysisAsync(readModel, Lucerne, r => Equals(r.Alerts, last), ct);
        Assert.Equal(last, result.Alerts);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_latest_analysis_query_returns_the_latest_event_of_every_kind()
    {
        var ct = TestContext.Current.CancellationToken;
        var (readModel, pipeline) = await StartAsync();
        var consensus = EnrichmentEvents.Consensus(Lucerne, Anchor);
        var alerts = EnrichmentEvents.Alerts(Lucerne, Anchor);
        var derived = EnrichmentEvents.Derived(Lucerne, Anchor);
        var trends = EnrichmentEvents.Trends(Lucerne, Anchor);
        var indices = EnrichmentEvents.Indices(Lucerne, Anchor);

        foreach (PipelineEvent evt in new PipelineEvent[] { consensus, alerts, derived, trends, indices })
        {
            await pipeline.OfferAsync(evt, ct);
        }

        var result = await AwaitAnalysisAsync(readModel, Lucerne, r => r.Indices is not null, ct);
        Assert.Multiple(
            () => Assert.Equal(Lucerne, result.Location),
            () => Assert.Equal(consensus, result.Consensus),
            () => Assert.Equal(alerts, result.Alerts),
            () => Assert.Equal(derived, result.Derived),
            () => Assert.Equal(trends, result.Trends),
            () => Assert.Equal(indices, result.Indices));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_newer_event_replaces_only_the_event_of_its_own_kind()
    {
        var ct = TestContext.Current.CancellationToken;
        var (readModel, pipeline) = await StartAsync();
        var trends = EnrichmentEvents.Trends(Lucerne, Anchor);
        var newerAlerts = EnrichmentEvents.Alerts(Lucerne, Anchor.AddHours(1));

        await pipeline.OfferAsync(EnrichmentEvents.Alerts(Lucerne, Anchor), ct);
        await pipeline.OfferAsync(trends, ct);
        await pipeline.OfferAsync(newerAlerts, ct);

        var result = await AwaitAnalysisAsync(readModel, Lucerne, r => Equals(r.Alerts, newerAlerts), ct);
        Assert.Multiple(
            () => Assert.Equal(newerAlerts, result.Alerts),
            () => Assert.Equal(trends, result.Trends),
            () => Assert.Null(result.Consensus),
            () => Assert.Null(result.Derived),
            () => Assert.Null(result.Indices));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Analysis_kinds_are_kept_per_location()
    {
        var ct = TestContext.Current.CancellationToken;
        var (readModel, pipeline) = await StartAsync();
        var lucerneAlerts = EnrichmentEvents.Alerts(Lucerne, Anchor);
        var zurichTrends = EnrichmentEvents.Trends(Zurich, Anchor);

        await pipeline.OfferAsync(lucerneAlerts, ct);
        await pipeline.OfferAsync(zurichTrends, ct);

        var zurich = await AwaitAnalysisAsync(readModel, Zurich, r => r.Trends is not null, ct);
        var lucerne = await AwaitAnalysisAsync(readModel, Lucerne, r => r.Alerts is not null, ct);
        Assert.Multiple(
            () => Assert.Equal(lucerneAlerts, lucerne.Alerts),
            () => Assert.Null(lucerne.Trends),
            () => Assert.Equal(zurichTrends, zurich.Trends),
            () => Assert.Null(zurich.Alerts));
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

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task The_read_model_keeps_consuming_when_no_streaming_client_is_attached()
    {
        var ct = TestContext.Current.CancellationToken;
        var (readModel, pipeline) = await StartWithPipelineHubAsync();
        var otherConsumer = await AttachOtherConsumerAsync(readModel, pipeline, ct);
        var events = CreateAlerts(50);

        await OfferAllAsync(pipeline.Queue, events, ct);

        await AssertReadModelAndOtherConsumerReceivedAllAsync(readModel, otherConsumer, events, ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_streaming_client_that_does_not_pull_never_holds_up_the_read_model()
    {
        var ct = TestContext.Current.CancellationToken;
        var (readModel, pipeline) = await StartWithPipelineHubAsync();
        var otherConsumer = await AttachOtherConsumerAsync(readModel, pipeline, ct);
        var idleClient = await RequestStreamAsync(readModel, ct);
        var events = CreateAlerts(300);

        await OfferAllAsync(pipeline.Queue, events, ct);

        await AssertReadModelAndOtherConsumerReceivedAllAsync(readModel, otherConsumer, events, ct);

        var client = new EventCollector();
        _ = idleClient.RunWith(client.AsSink(), Sys.Materializer());
        var received = await client.WaitUntilAsync(
            r => r.Count > 0 && Equals(r[^1], events[^1]), "the idle client to receive the last event", ct);

        Assert.Multiple(
            () => Assert.InRange(received.Count, 1, 256),
            () => Assert.Equal(events.TakeLast(received.Count), received));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_streaming_client_that_keeps_up_receives_every_event_in_order()
    {
        var ct = TestContext.Current.CancellationToken;
        var (readModel, pipeline) = await StartWithPipelineHubAsync();
        var client = new EventCollector();
        var clientSource = await RequestStreamAsync(readModel, ct);
        _ = clientSource.RunWith(client.AsSink(), Sys.Materializer());
        await WarmUpAsync(pipeline.Queue, client, ct);
        var events = CreateAlerts(50);

        await OfferAllAsync(pipeline.Queue, events, ct);

        var received = await client.WaitUntilAsync(r => r.Count >= events.Count, "50 events at the client", ct);
        Assert.Equal(events, received);
    }

    // Polls the read model in real time (not dilated by the TestKit timefactor) until the condition holds.
    private static async Task<LatestAnalysisResult> AwaitAnalysisAsync(
        IActorRef readModel, LocationId location, Func<LatestAnalysisResult, bool> condition, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow + TestTimeouts.RealTimeWaitMax;
        LatestAnalysisResponse? last = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            last = await readModel.Ask<LatestAnalysisResponse>(new QueryLatestAnalysis(location), AskTimeout, ct);
            if (last is LatestAnalysisResult result && condition(result))
            {
                return result;
            }

            await Task.Delay(20, ct);
        }

        Assert.Fail($"Timed out after {TestTimeouts.RealTimeWaitMax} waiting for the analysis of {location}; last reply: {last}");
        throw new System.Diagnostics.UnreachableException();
    }

    private static readonly PipelineEvent WarmUp =
        new PipelineEvent.AlertComputed(new AlertResult(Lucerne, []), Anchor.AddDays(-1));

    private static List<PipelineEvent> CreateAlerts(int count) =>
        Enumerable.Range(1, count)
            .Select(i => (PipelineEvent)new PipelineEvent.AlertComputed(new AlertResult(Lucerne, []), Anchor.AddMinutes(i)))
            .ToList();

    // Attaches a second consumer to the test pipeline hub next to the read model, and proves with a warm-up
    // event that both are attached before the spec offers its events.
    private async Task<EventCollector> AttachOtherConsumerAsync(
        IActorRef readModel, TestPipeline pipeline, CancellationToken ct)
    {
        var collector = new EventCollector();
        _ = pipeline.HubSource.RunWith(collector.AsSink(), Sys.Materializer());
        await WarmUpAsync(pipeline.Queue, collector, ct);
        await AwaitAssertAsync(async () =>
        {
            var response = await readModel.Ask<LatestAnalysisResponse>(new QueryLatestAnalysis(Lucerne), AskTimeout, ct);
            var result = Assert.IsType<LatestAnalysisResult>(response);
            Assert.Equal(WarmUp, result.Alerts);
        }, TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        return collector;
    }

    // A consumer attaches to a hub asynchronously: offer warm-up events until the consumer sees one.
    private async Task WarmUpAsync(
        ISourceQueueWithComplete<PipelineEvent> queue, EventCollector consumer, CancellationToken ct)
    {
        await AwaitAssertAsync(async () =>
        {
            await queue.OfferAsync(WarmUp, ct);
            await consumer.WarmUpSeen.WaitAsync(TimeSpan.FromMilliseconds(500), ct);
        }, TestTimeouts.AwaitAssertMax, cancellationToken: ct);
    }

    private static async Task OfferAllAsync(
        ISourceQueueWithComplete<PipelineEvent> queue, IReadOnlyList<PipelineEvent> events, CancellationToken ct)
    {
        var offered = 0;

        async Task OfferInOrder()
        {
            foreach (var evt in events)
            {
                var result = await queue.OfferAsync(evt, ct);
                Assert.IsType<QueueOfferResult.Enqueued>(result);
                Interlocked.Increment(ref offered);
            }
        }

        // A stalled pipeline backpressures the queue; fail with a bounded wait instead of hanging.
        try
        {
            await OfferInOrder().WaitAsync(TestTimeouts.RealTimeWaitMax, ct);
        }
        catch (TimeoutException)
        {
            Assert.Fail($"The pipeline stalled: only {Volatile.Read(ref offered)} of {events.Count} events " +
                        $"were accepted within {TestTimeouts.RealTimeWaitMax}");
        }
    }

    private async Task AssertReadModelAndOtherConsumerReceivedAllAsync(
        IActorRef readModel, EventCollector otherConsumer, IReadOnlyList<PipelineEvent> events, CancellationToken ct)
    {
        var received = await otherConsumer.WaitUntilAsync(
            r => r.Count >= events.Count, $"{events.Count} events at the other pipeline consumer", ct);
        Assert.Equal(events, received);

        var response = await readModel.Ask<LatestAnalysisResponse>(new QueryLatestAnalysis(Lucerne), AskTimeout, ct);
        var result = Assert.IsType<LatestAnalysisResult>(response);
        Assert.Equal(events[^1], result.Alerts);
    }

    // Records every non-warm-up event a stream delivers and lets a spec wait, bounded, for a condition on them.
    private sealed class EventCollector
    {
        private readonly Lock _gate = new();
        private readonly List<PipelineEvent> _events = [];
        private readonly TaskCompletionSource _warmUpSeen = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Func<IReadOnlyList<PipelineEvent>, bool>? _condition;
        private TaskCompletionSource? _conditionMet;

        public Sink<PipelineEvent, Task<Akka.Done>> AsSink() => Sink.ForEach<PipelineEvent>(Add);

        public Task WarmUpSeen => _warmUpSeen.Task;

        public async Task<IReadOnlyList<PipelineEvent>> WaitUntilAsync(
            Func<IReadOnlyList<PipelineEvent>, bool> condition, string description, CancellationToken ct)
        {
            var met = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_gate)
            {
                _condition = condition;
                _conditionMet = met;
                if (condition(_events))
                {
                    met.TrySetResult();
                }
            }

            try
            {
                await met.Task.WaitAsync(TestTimeouts.RealTimeWaitMax, ct);
            }
            catch (TimeoutException)
            {
                Assert.Fail($"Timed out after {TestTimeouts.RealTimeWaitMax} waiting for {description}; " +
                            $"received {Snapshot().Count} events");
            }

            return Snapshot();
        }

        private void Add(PipelineEvent evt)
        {
            if (Equals(evt, WarmUp))
            {
                _warmUpSeen.TrySetResult();
                return;
            }

            lock (_gate)
            {
                _events.Add(evt);
                if (_condition is not null && _condition(_events))
                {
                    _conditionMet?.TrySetResult();
                }
            }
        }

        private List<PipelineEvent> Snapshot()
        {
            lock (_gate)
            {
                return [.. _events];
            }
        }
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

    private sealed record TestPipeline(
        ISourceQueueWithComplete<PipelineEvent> Queue,
        Source<PipelineEvent, Akka.NotUsed> HubSource);

    // Mirrors ForecastPipeline's output: a BroadcastHub (buffer size 2) that every consumer reads from,
    // so a stalled read model holds up the other consumers of the hub as it would in production.
    private sealed class FakePipeline : ReceiveActor
    {
        public FakePipeline(IMaterializer mat, TaskCompletionSource<TestPipeline> ready)
        {
            var (queue, hubSource) = Source.Queue<PipelineEvent>(32, OverflowStrategy.Backpressure)
                .ToMaterialized(BroadcastHub.Sink<PipelineEvent>(bufferSize: 2), Keep.Both)
                .Run(mat);
            var pipeline = new TestPipeline(queue, hubSource);

            Receive<RequestPipelineOutput>(msg =>
            {
                hubSource
                    .RunWith(StreamRefs.SourceRef<PipelineEvent>(), mat)
                    .PipeTo(Sender, Self,
                        sr => new RequestPipelineOutputCompleted(msg.RequestId, sr),
                        ex => new RequestPipelineOutputFailed(msg.RequestId, ex));
                ready.TrySetResult(pipeline);
            });
        }
    }
}
