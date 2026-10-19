using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Core.Actors;
using Njord.Messages.Alerts;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public sealed class GrpcConsumerSpec : NjordActorSpec
{
    private static readonly DateTimeOffset Anchor = TestTimeConstants.NoonAnchor;
    private static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(3);
    private static readonly Location Lucerne = new("lucerne", 0, 0);
    private static readonly Location Zurich = new("zurich", 0, 0);

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

        var readModel = Sys.ActorOf(Props.Create(() => new GrpcConsumer()), $"grpc-consumer-{Guid.NewGuid():N}");
        var testPipeline = await ready.Task.WaitAsync(TestTimeouts.RealTimeWaitMax, TestContext.Current.CancellationToken);
        return (readModel, testPipeline);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_domain_data_query_returns_the_latest_event_of_the_location_and_domain()
    {
        var ct = TestContext.Current.CancellationToken;
        var (readModel, pipeline) = await StartAsync();
        var older = new PipelineEvent.WeatherDataReceived(DomainData.Lucerne, DomainData.Weather(Anchor, 2));
        var newer = new PipelineEvent.WeatherDataReceived(DomainData.Lucerne, DomainData.Weather(Anchor.AddHours(1), 2));
        var flood = new PipelineEvent.FloodDataReceived(DomainData.Lucerne, DomainData.Flood(Anchor));

        await pipeline.OfferAsync(older, ct);
        await pipeline.OfferAsync(flood, ct);
        await pipeline.OfferAsync(newer, ct);

        await AwaitAssertAsync(async () =>
        {
            var weather = await readModel.Ask<DomainDataResponse>(new QueryDomainData(Lucerne, DomainKind.Weather), AskTimeout, ct);
            var floodReply = await readModel.Ask<DomainDataResponse>(new QueryDomainData(Lucerne, DomainKind.Flood), AskTimeout, ct);
            Assert.Multiple(
                () => Assert.Equal(new DomainDataResult(newer), weather),
                () => Assert.Equal(new DomainDataResult(flood), floodReply));
        }, TestTimeouts.AwaitAssertMax, cancellationToken: ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Domain_data_is_kept_per_location()
    {
        var ct = TestContext.Current.CancellationToken;
        var (readModel, pipeline) = await StartAsync();
        var lucerne = new PipelineEvent.WeatherDataReceived(DomainData.Lucerne, DomainData.Weather(Anchor, 2));

        await pipeline.OfferAsync(lucerne, ct);

        await AwaitAssertAsync(async () =>
        {
            var found = await readModel.Ask<DomainDataResponse>(new QueryDomainData(new Location("LUCERNE", 0, 0), DomainKind.Weather), AskTimeout, ct);
            var missing = await readModel.Ask<DomainDataResponse>(new QueryDomainData(Zurich, DomainKind.Weather), AskTimeout, ct);
            Assert.Multiple(
                () => Assert.Equal(new DomainDataResult(lucerne), found),
                () => Assert.Equal(new DomainDataNotAvailable(Zurich, DomainKind.Weather), missing));
        }, TestTimeouts.AwaitAssertMax, cancellationToken: ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Raised_alerts_are_merged_per_type_and_location()
    {
        var ct = TestContext.Current.CancellationToken;
        var (readModel, pipeline) = await StartAsync();
        var frost = new Alert(AlertType.Frost, AlertSeverity.Yellow, 0.6, NoDetails.Instance);
        var heat = new Alert(AlertType.Heat, AlertSeverity.Orange, 0.9, NoDetails.Instance);
        var frostRed = frost with { Severity = AlertSeverity.Red };

        await pipeline.OfferAsync(new PipelineEvent.AlertRaised(DomainData.Lucerne, frost, Anchor), ct);
        await pipeline.OfferAsync(new PipelineEvent.AlertRaised(DomainData.Lucerne, heat, Anchor.AddMinutes(1)), ct);
        await pipeline.OfferAsync(new PipelineEvent.AlertRaised(DomainData.Lucerne, frostRed, Anchor.AddMinutes(2)), ct);

        var result = await AwaitAnalysisAsync(readModel, Lucerne, r => r.Alerts?.Timestamp == Anchor.AddMinutes(2), ct);
        Assert.NotNull(result.Alerts);
        var alerts = result.Alerts.Result.Alerts;
        Assert.Equal([frostRed, heat], alerts);
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
    public async Task Alerts_are_kept_per_location()
    {
        var ct = TestContext.Current.CancellationToken;
        var (readModel, pipeline) = await StartAsync();
        var lucerneAlerts = EnrichmentEvents.Alerts(Lucerne, Anchor);
        var zurichAlerts = EnrichmentEvents.Alerts(Zurich, Anchor.AddHours(1));

        await pipeline.OfferAsync(lucerneAlerts, ct);
        await pipeline.OfferAsync(zurichAlerts, ct);

        var zurich = await AwaitAnalysisAsync(readModel, Zurich, r => r.Alerts is not null, ct);
        var lucerne = await AwaitAnalysisAsync(readModel, Lucerne, r => r.Alerts is not null, ct);
        Assert.Multiple(
            () => Assert.Equal(lucerneAlerts, lucerne.Alerts),
            () => Assert.Equal(zurichAlerts, zurich.Alerts));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Queries_before_the_first_cycle_report_that_no_data_is_available()
    {
        var ct = TestContext.Current.CancellationToken;
        var (readModel, _) = await StartAsync();

        var domainData = await readModel.Ask<DomainDataResponse>(new QueryDomainData(Lucerne, DomainKind.Weather), AskTimeout, ct);
        var analysis = await readModel.Ask<LatestAnalysisResponse>(new QueryLatestAnalysis(Lucerne), AskTimeout, ct);

        Assert.Equal(new DomainDataNotAvailable(Lucerne, DomainKind.Weather), domainData);
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
    public async Task A_stream_requested_before_the_first_subscription_is_answered_at_once()
    {
        var ct = TestContext.Current.CancellationToken;
        var pipeline = CreateTestProbe();
        ActorRegistry.Register<IForecastPipeline>(pipeline, overwrite: true);
        var readModel = Sys.ActorOf(Props.Create(() => new GrpcConsumer()), $"grpc-consumer-{Guid.NewGuid():N}");
        var request = await pipeline.ExpectMsgAsync<RequestPipelineOutput>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);

        var response = await readModel.Ask<RequestGrpcStreamResponse>(new RequestGrpcStream(7), AskTimeout, ct);

        var completed = Assert.IsType<RequestGrpcStreamCompleted>(response);
        Assert.Equal(7, completed.RequestId);
        var client = new EventCollector();
        _ = completed.SourceRef.Source.RunWith(client.AsSink(), Sys.Materializer());
        var (queue, source) = Source.Queue<PipelineEvent>(16, OverflowStrategy.Backpressure).PreMaterialize(Sys.Materializer());
        var sourceRef = await source.RunWith(StreamRefs.SourceRef<PipelineEvent>(), Sys.Materializer());
        readModel.Tell(new RequestPipelineOutputCompleted(request.RequestId, sourceRef), pipeline.Ref);
        await WarmUpAsync(queue, client, ct);
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

    private async Task<LatestAnalysisResult> AwaitAnalysisAsync(
        IActorRef readModel, Location location, Func<LatestAnalysisResult, bool> condition, CancellationToken ct)
    {
        LatestAnalysisResult? matched = null;
        await AwaitAssertAsync(async () =>
        {
            var reply = await readModel.Ask<LatestAnalysisResponse>(new QueryLatestAnalysis(location), AskTimeout, ct);
            var result = Assert.IsType<LatestAnalysisResult>(reply);
            Assert.True(condition(result), $"The analysis of {location} does not satisfy the condition yet: {result}");
            matched = result;
        }, TestTimeouts.AwaitAssertMax, cancellationToken: ct);

        return Assert.IsType<LatestAnalysisResult>(matched);
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
                            $"received {ReceivedEvents().Count} events");
            }

            return ReceivedEvents();
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

        private List<PipelineEvent> ReceivedEvents()
        {
            lock (_gate)
            {
                return [.. _events];
            }
        }
    }

    private async Task<Source<PipelineEvent, Akka.NotUsed>> RequestStreamAsync(IActorRef readModel, CancellationToken ct)
    {
        RequestGrpcStreamCompleted? completed = null;
        var attempt = 0;
        await AwaitAssertAsync(async () =>
        {
            var response = await readModel.Ask<RequestGrpcStreamResponse>(new RequestGrpcStream(attempt++), AskTimeout, ct);
            completed = Assert.IsType<RequestGrpcStreamCompleted>(response);
        }, TestTimeouts.AwaitAssertMax, cancellationToken: ct);

        return Assert.IsType<RequestGrpcStreamCompleted>(completed).SourceRef.Source;
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
