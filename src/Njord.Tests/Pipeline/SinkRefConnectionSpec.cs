using Akka;
using Akka.Actor;
using Akka.Hosting;
using Akka.Persistence;
using Akka.Persistence.TestKit;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Pipeline;
using Njord.Tests.Shared;
using Servus.Akka;

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
            Receive<RequestSourceRef>(_ =>
            {
                Sender.Tell(new SourceRefResponse());
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

    [Fact(Timeout = 5000)]
    public async Task Persistent_actor_can_offer_after_recovery_via_sinkref()
    {
        var received = new TaskCompletionSource<int>();

        var pipeline = Sys.ActorOf(Props.Create(() =>
            new FakePipelineWithFullGraph(Mat, received)));

        var registry = ActorRegistry.For(Sys);
        registry.Register<PipelineActor>(pipeline, overwrite: true);

        var consumer = Sys.ActorOf(Props.Create(() =>
            new PersistentConsumerActor($"consumer-{Guid.NewGuid():N}")));

        var value = await received.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(99, value);
    }

    [Fact(Timeout = 5000)]
    public async Task Persistent_actor_with_recovery_events_can_offer_via_sinkref()
    {
        var persistenceId = $"consumer-{Guid.NewGuid():N}";
        var received = new TaskCompletionSource<int>();

        var pipeline = Sys.ActorOf(Props.Create(() =>
            new FakePipelineWithFullGraph(Mat, received)));

        var registry = ActorRegistry.For(Sys);
        registry.Register<PipelineActor>(pipeline, overwrite: true);

        var first = Sys.ActorOf(Props.Create(() =>
            new PersistentConsumerActor(persistenceId)));

        await AsyncAssert.WaitUntil(() => received.Task.IsCompleted);
        received = new TaskCompletionSource<int>();

        await first.GracefulStop(TimeSpan.FromSeconds(2));

        var pipeline2 = Sys.ActorOf(Props.Create(() =>
            new FakePipelineWithFullGraph(Mat, received)));
        registry.Register<PipelineActor>(pipeline2, overwrite: true);

        var second = Sys.ActorOf(Props.Create(() =>
            new PersistentConsumerActor(persistenceId)));

        var value = await received.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(99, value);
    }

    private sealed class PersistentConsumerActor : ReceivePersistentActor
    {
        public override string PersistenceId { get; }

        private readonly IMaterializer _mat = Context.Materializer();
        private ISourceQueueWithComplete<int>? _queue;
        private bool _sourceReceived;

        private sealed record ConnectionEstablished;
        private sealed record OfferFailed(Exception Error);
        private sealed record Evt(int Value);

        public PersistentConsumerActor(string persistenceId)
        {
            PersistenceId = persistenceId;

            Recover<Evt>(_ => { });
            Recover<SnapshotOffer>(_ => { });

            WaitingForRefs();
        }

        protected override void PreStart()
        {
            var pipeline = Context.GetActor<PipelineActor>();
            pipeline.Tell(new RequestSinkRef());
            pipeline.Tell(new RequestSourceRef());
        }

        private void WaitingForRefs()
        {
            Command<SinkRefResponse>(response =>
            {
                _queue = Source.Queue<int>(4, OverflowStrategy.Backpressure)
                    .To(response.SinkRef.Sink)
                    .Run(_mat);
                TryConnect();
            });
            Command<SourceRefResponse>(_ =>
            {
                _sourceReceived = true;
                TryConnect();
            });
            CommandAny(_ => Stash.Stash());
        }

        private void TryConnect()
        {
            if (_queue is null || !_sourceReceived)
                return;

            Self.Tell(new DoOffer());
            Become(Connecting);
        }

        private sealed record DoOffer;

        private void Connecting()
        {
            Command<DoOffer>(_ =>
            {
                _queue!.OfferAsync(99).PipeTo(Self,
                    success: _ => new ConnectionEstablished(),
                    failure: ex => new OfferFailed(ex));
                Become(WaitingForConnection);
            });
            CommandAny(_ => Stash.Stash());
        }

        private void WaitingForConnection()
        {
            Command<ConnectionEstablished>(_ =>
            {
                Become(Ready);
                Stash.UnstashAll();
            });
            Command<OfferFailed>(msg =>
            {
                Self.Tell(new DoOffer());
                Become(Connecting);
            });
            CommandAny(_ => Stash.Stash());
        }

        private void Ready()
        {
            CommandAny(_ => { });
        }
    }

    private sealed record RequestSourceRef;
    private sealed record SourceRefResponse;

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
