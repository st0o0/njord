using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Core;
using Njord.Core.Actors;
using Njord.Messages.Alerts;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public abstract class GrpcConsumerSpecBase : NjordActorSpec
{
    protected static readonly DateTimeOffset Anchor = TestTimeConstants.NoonAnchor;
    protected static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(3);
    protected static readonly Location Lucerne = new("lucerne", 0, 0);
    protected static readonly Location Zurich = new("zurich", 0, 0);
    internal static readonly PipelineEvent.AlertRaised WarmUp = EnrichmentEvents.Alerts(Lucerne, Anchor.AddDays(-1));
    private static readonly Alert HeatAlert = new(AlertType.Heat, AlertSeverity.Yellow, 0.75, NoDetails.Instance);

    protected override bool UseTestPersistence => false;


    protected async Task<(IActorRef ReadModel, ISourceQueueWithComplete<PipelineEvent> Pipeline)> StartAsync()
    {
        var (readModel, pipeline) = await StartWithPipelineHubAsync();
        return (readModel, pipeline.Queue);
    }

    protected async Task<(IActorRef ReadModel, TestPipeline Pipeline)> StartWithPipelineHubAsync()
    {
        var ready = new TaskCompletionSource<TestPipeline>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pipeline = Sys.ActorOf(Props.Create(() => new FakePipeline(Sys.Materializer(), ready)),
            $"pipeline-{Guid.NewGuid():N}");
        ActorRegistry.Register<IForecastPipeline>(pipeline, overwrite: true);

        var readModel = Sys.ActorOf(Props.Create(() => new GrpcConsumer()), $"grpc-consumer-{Guid.NewGuid():N}");
        var testPipeline = await ready.Task.WaitAsync(TestTimeouts.RealTimeWaitMax, TestContext.Current.CancellationToken);
        return (readModel, testPipeline);
    }

    protected async Task<LatestAnalysisResult> AwaitAnalysisAsync(
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

    protected static void AssertAlerts(Location location, Alert[] expected, AlertResult? actual)
    {
        Assert.NotNull(actual);
        Assert.Multiple(
            () => Assert.Equal(location, actual.Location),
            () => Assert.Equal(expected, actual.Alerts));
    }

    protected static List<PipelineEvent> CreateAlerts(int count) =>
        Enumerable.Range(1, count)
            .Select(i => (PipelineEvent)(EnrichmentEvents.Alerts(Lucerne, Anchor.AddMinutes(i)) with { Alert = HeatAlert with { Confidence = i } }))
            .ToList();

    // Attaches a second consumer to the test pipeline hub next to the read model, and proves with a warm-up
    // event that both are attached before the spec offers its events.
    protected async Task<EventCollector> AttachOtherConsumerAsync(
        IActorRef readModel, TestPipeline pipeline, CancellationToken ct)
    {
        var collector = new EventCollector();
        _ = pipeline.HubSource.RunWith(collector.AsSink(), Sys.Materializer());
        await WarmUpAsync(pipeline.Queue, collector, ct);
        await AwaitAssertAsync(async () =>
        {
            var response = await readModel.Ask<LatestAnalysisResponse>(new QueryLatestAnalysis(Lucerne), AskTimeout, ct);
            var result = Assert.IsType<LatestAnalysisResult>(response);
            AssertAlerts(Lucerne, [WarmUp.Alert], result.Alerts);
        }, TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        return collector;
    }

    // A consumer attaches to a hub asynchronously: offer warm-up events until the consumer sees one.
    protected async Task WarmUpAsync(
        ISourceQueueWithComplete<PipelineEvent> queue, EventCollector consumer, CancellationToken ct)
    {
        await AwaitAssertAsync(async () =>
        {
            await queue.OfferAsync(WarmUp, ct);
            await consumer.WarmUpSeen.WaitAsync(TimeSpan.FromMilliseconds(500), ct);
        }, TestTimeouts.AwaitAssertMax, cancellationToken: ct);
    }

    protected static async Task OfferAllAsync(
        ISourceQueueWithComplete<PipelineEvent> queue, IReadOnlyList<PipelineEvent> events, CancellationToken ct)
    {
        var offered = 0;

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

        return;

        async Task OfferInOrder()
        {
            foreach (var evt in events)
            {
                var result = await queue.OfferAsync(evt, ct);
                Assert.IsType<QueueOfferResult.Enqueued>(result);
                Interlocked.Increment(ref offered);
            }
        }
    }

    protected async Task AssertReadModelAndOtherConsumerReceivedAllAsync(
        IActorRef readModel, EventCollector otherConsumer, IReadOnlyList<PipelineEvent> events, CancellationToken ct)
    {
        var received = await otherConsumer.WaitUntilAsync(
            r => r.Count >= events.Count, $"{events.Count} events at the other pipeline consumer", ct);
        Assert.Equal(events, received);

        var response = await readModel.Ask<LatestAnalysisResponse>(new QueryLatestAnalysis(Lucerne), AskTimeout, ct);
        var result = Assert.IsType<LatestAnalysisResult>(response);
        var last = Assert.IsType<PipelineEvent.AlertRaised>(events[^1]);
        AssertAlerts(Lucerne, [last.Alert], result.Alerts);
    }

    protected async Task<Source<PipelineEvent, Akka.NotUsed>> RequestStreamAsync(IActorRef readModel, CancellationToken ct)
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
}
