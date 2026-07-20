using Akka;
using Akka.Actor;
using Akka.Persistence.TestKit;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Pipeline;

namespace Njord.Tests.Pipeline;

public sealed class SinkRefConnectionSpec : PersistenceTestKit
{
    private IMaterializer Mat => Sys.Materializer();

    [Fact(Timeout = 5000)]
    public async Task OfferAsync_completes_through_sinkref_to_mergehub_with_broadcasthub()
    {
        var received = new TaskCompletionSource<int>();

        var (mergeHubSink, mergeHubSource) = MergeHub.Source<int>(perProducerBufferSize: 16)
            .PreMaterialize(Mat);

        var (broadcastHubSource, broadcastHubSink) = BroadcastHub.Sink<int>(bufferSize: 2)
            .PreMaterialize(Mat);

        mergeHubSource
            .Buffer(32, OverflowStrategy.Backpressure)
            .To(broadcastHubSink)
            .Run(Mat);

        broadcastHubSource
            .RunWith(Sink.ForEach<int>(x => received.TrySetResult(x)), Mat);

        var sinkRef = await StreamRefs.SinkRef<int>()
            .To(mergeHubSink)
            .Run(Mat);

        var queue = Source.Queue<int>(4, OverflowStrategy.Backpressure)
            .To(sinkRef.Sink)
            .Run(Mat);

        var result = await queue.OfferAsync(42);

        Assert.Equal(QueueOfferResult.Enqueued.Instance, result);
    }

    [Fact(Timeout = 5000)]
    public async Task OfferAsync_completes_with_throttle_between_mergehub_and_broadcasthub()
    {
        var received = new TaskCompletionSource<int>();

        var (mergeHubSink, mergeHubSource) = MergeHub.Source<int>(perProducerBufferSize: 16)
            .PreMaterialize(Mat);

        var (broadcastHubSource, broadcastHubSink) = BroadcastHub.Sink<int>(bufferSize: 2)
            .PreMaterialize(Mat);

        mergeHubSource
            .SelectAsyncUnordered(2, async x =>
            {
                await Task.Delay(10);
                return x;
            })
            .Buffer(32, OverflowStrategy.Backpressure)
            .To(broadcastHubSink)
            .Run(Mat);

        broadcastHubSource
            .RunWith(Sink.ForEach<int>(x => received.TrySetResult(x)), Mat);

        var sinkRef = await StreamRefs.SinkRef<int>()
            .To(mergeHubSink)
            .Run(Mat);

        var queue = Source.Queue<int>(4, OverflowStrategy.Backpressure)
            .To(sinkRef.Sink)
            .Run(Mat);

        var result = await queue.OfferAsync(42);

        Assert.Equal(QueueOfferResult.Enqueued.Instance, result);
    }

    [Fact(Timeout = 5000)]
    public async Task OfferAsync_completes_with_no_broadcasthub_consumers()
    {
        var (mergeHubSink, mergeHubSource) = MergeHub.Source<int>(perProducerBufferSize: 16)
            .PreMaterialize(Mat);

        var (_, broadcastHubSink) = BroadcastHub.Sink<int>(bufferSize: 2)
            .PreMaterialize(Mat);

        mergeHubSource
            .Buffer(32, OverflowStrategy.Backpressure)
            .To(broadcastHubSink)
            .Run(Mat);

        var sinkRef = await StreamRefs.SinkRef<int>()
            .To(mergeHubSink)
            .Run(Mat);

        var queue = Source.Queue<int>(4, OverflowStrategy.Backpressure)
            .To(sinkRef.Sink)
            .Run(Mat);

        var result = await queue.OfferAsync(42);

        Assert.Equal(QueueOfferResult.Enqueued.Instance, result);
    }

    [Fact(Timeout = 5000)]
    public async Task OfferAsync_completes_with_ask_consumer_and_no_responder()
    {
        var blackhole = Sys.ActorOf(Props.Create(() => new BlackholeActor()));

        var (mergeHubSink, mergeHubSource) = MergeHub.Source<int>(perProducerBufferSize: 16)
            .PreMaterialize(Mat);

        var (broadcastHubSource, broadcastHubSink) = BroadcastHub.Sink<int>(bufferSize: 2)
            .PreMaterialize(Mat);

        mergeHubSource
            .SelectAsyncUnordered(2, async x =>
            {
                await Task.Delay(10);
                return x;
            })
            .Buffer(32, OverflowStrategy.Backpressure)
            .To(broadcastHubSink)
            .Run(Mat);

        broadcastHubSource
            .Ask<string>(blackhole, TimeSpan.FromSeconds(5))
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(
                _ => Akka.Streams.Supervision.Directive.Resume))
            .To(Sink.Ignore<string>())
            .Run(Mat);

        var sinkRef = await StreamRefs.SinkRef<int>()
            .To(mergeHubSink)
            .Run(Mat);

        var queue = Source.Queue<int>(4, OverflowStrategy.Backpressure)
            .To(sinkRef.Sink)
            .Run(Mat);

        var result = await queue.OfferAsync(42);

        Assert.Equal(QueueOfferResult.Enqueued.Instance, result);
    }

    [Fact(Timeout = 5000)]
    public async Task OfferAsync_completes_with_real_budget_throttle()
    {
        var received = new TaskCompletionSource<int>();
        var gate = new AlwaysAllowGate();

        var (mergeHubSink, mergeHubSource) = MergeHub.Source<int>(perProducerBufferSize: 16)
            .PreMaterialize(Mat);

        var (broadcastHubSource, broadcastHubSink) = BroadcastHub.Sink<int>(bufferSize: 2)
            .PreMaterialize(Mat);

        mergeHubSource
            .Via(new BudgetThrottleStage<int>(gate))
            .SelectAsyncUnordered(2, x => Task.FromResult(x))
            .Buffer(32, OverflowStrategy.Backpressure)
            .To(broadcastHubSink)
            .Run(Mat);

        broadcastHubSource
            .RunWith(Sink.ForEach<int>(x => received.TrySetResult(x)), Mat);

        var sinkRef = await StreamRefs.SinkRef<int>()
            .To(mergeHubSink)
            .Run(Mat);

        var queue = Source.Queue<int>(4, OverflowStrategy.Backpressure)
            .To(sinkRef.Sink)
            .Run(Mat);

        var result = await queue.OfferAsync(42);
        Assert.Equal(QueueOfferResult.Enqueued.Instance, result);

        var value = await received.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(42, value);
    }

    [Fact(Timeout = 5000)]
    public async Task OfferAsync_completes_across_actor_boundary()
    {
        var received = new TaskCompletionSource<int>();

        var pipeline = Sys.ActorOf(Props.Create(() =>
            new FakePipelineWithFullGraph(Mat, received)));

        var response = await pipeline.Ask<SinkRefResponse>(
            new RequestSinkRef(), TimeSpan.FromSeconds(2));

        var queue = Source.Queue<int>(4, OverflowStrategy.Backpressure)
            .To(response.SinkRef.Sink)
            .Run(Mat);

        var result = await queue.OfferAsync(42);

        Assert.Equal(QueueOfferResult.Enqueued.Instance, result);

        var value = await received.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(42, value);
    }

    [Fact(Timeout = 5000)]
    public async Task OfferAsync_completes_across_actor_boundary_with_budget_throttle()
    {
        var received = new TaskCompletionSource<int>();
        var gate = new AlwaysAllowGate();

        var pipeline = Sys.ActorOf(Props.Create(() =>
            new ThrottledPipelineActor(Mat, received, gate)));

        var response = await pipeline.Ask<SinkRefResponse>(
            new RequestSinkRef(), TimeSpan.FromSeconds(2));

        var queue = Source.Queue<int>(4, OverflowStrategy.Backpressure)
            .To(response.SinkRef.Sink)
            .Run(Mat);

        var result = await queue.OfferAsync(42);

        Assert.Equal(QueueOfferResult.Enqueued.Instance, result);

        var value = await received.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(42, value);
    }

    private sealed record RequestSinkRef;
    private sealed record SinkRefResponse(ISinkRef<int> SinkRef);

    private sealed class FakePipelineWithFullGraph : ReceiveActor
    {
        public FakePipelineWithFullGraph(IMaterializer mat, TaskCompletionSource<int> received)
        {
            var (mergeHubSink, mergeHubSource) = MergeHub.Source<int>(perProducerBufferSize: 16)
                .PreMaterialize(mat);

            var (broadcastHubSource, broadcastHubSink) = BroadcastHub.Sink<int>(bufferSize: 2)
                .PreMaterialize(mat);

            mergeHubSource
                .SelectAsyncUnordered(2, async x =>
                {
                    await Task.Delay(10);
                    return x;
                })
                .Buffer(32, OverflowStrategy.Backpressure)
                .To(broadcastHubSink)
                .Run(mat);

            broadcastHubSource
                .RunWith(Sink.ForEach<int>(x => received.TrySetResult(x)), mat);

            Receive<RequestSinkRef>(_ =>
            {
                StreamRefs.SinkRef<int>()
                    .To(mergeHubSink)
                    .Run(mat)
                    .PipeTo(Sender, Self,
                        sr => new SinkRefResponse(sr),
                        _ => null!);
            });
        }
    }

    private sealed class ThrottledPipelineActor : ReceiveActor
    {
        public ThrottledPipelineActor(IMaterializer mat, TaskCompletionSource<int> received, IBudgetGate<int> gate)
        {
            var (mergeHubSink, mergeHubSource) = MergeHub.Source<int>(perProducerBufferSize: 16)
                .PreMaterialize(mat);

            var (broadcastHubSource, broadcastHubSink) = BroadcastHub.Sink<int>(bufferSize: 2)
                .PreMaterialize(mat);

            mergeHubSource
                .Via(new BudgetThrottleStage<int>(gate))
                .SelectAsyncUnordered(2, x => Task.FromResult(x))
                .Buffer(32, OverflowStrategy.Backpressure)
                .To(broadcastHubSink)
                .Run(mat);

            broadcastHubSource
                .RunWith(Sink.ForEach<int>(x => received.TrySetResult(x)), mat);

            Receive<RequestSinkRef>(_ =>
            {
                StreamRefs.SinkRef<int>()
                    .To(mergeHubSink)
                    .Run(mat)
                    .PipeTo(Sender, Self,
                        sr => new SinkRefResponse(sr),
                        _ => null!);
            });
        }
    }

    private sealed class AlwaysAllowGate : IBudgetGate<int>
    {
        public bool TryAcquire(int element) => true;
        public TimeSpan EstimateDelay(int element) => TimeSpan.Zero;
    }

    private sealed class BlackholeActor : ReceiveActor
    {
        public BlackholeActor()
        {
            ReceiveAny(_ => { });
        }
    }
}
