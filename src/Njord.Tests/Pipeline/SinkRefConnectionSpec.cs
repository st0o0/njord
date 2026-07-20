using Akka;
using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Pipeline;

namespace Njord.Tests.Pipeline;

public sealed class SinkRefConnectionSpec : IAsyncLifetime
{
    private ActorSystem _system = null!;
    private IMaterializer _mat = null!;

    public ValueTask InitializeAsync()
    {
        _system = ActorSystem.Create("sinkref-spec-" + Guid.NewGuid().ToString("N")[..8]);
        _mat = _system.Materializer();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _system.Terminate();
    }

    [Fact(Timeout = 5000)]
    public async Task OfferAsync_completes_through_sinkref_to_mergehub_with_broadcasthub()
    {
        var received = new List<int>();

        var (mergeHubSink, mergeHubSource) = MergeHub.Source<int>(perProducerBufferSize: 16)
            .PreMaterialize(_mat);

        var (broadcastHubSource, broadcastHubSink) = BroadcastHub.Sink<int>(bufferSize: 2)
            .PreMaterialize(_mat);

        mergeHubSource
            .Buffer(32, OverflowStrategy.Backpressure)
            .To(broadcastHubSink)
            .Run(_mat);

        broadcastHubSource
            .RunWith(Sink.ForEach<int>(x => received.Add(x)), _mat);

        var sinkRef = await StreamRefs.SinkRef<int>()
            .To(mergeHubSink)
            .Run(_mat);

        var queue = Source.Queue<int>(4, OverflowStrategy.Backpressure)
            .To(sinkRef.Sink)
            .Run(_mat);

        var result = await queue.OfferAsync(42);

        Assert.Equal(QueueOfferResult.Enqueued.Instance, result);
    }

    [Fact(Timeout = 5000)]
    public async Task OfferAsync_completes_with_throttle_stage_between_mergehub_and_broadcasthub()
    {
        var received = new List<int>();

        var (mergeHubSink, mergeHubSource) = MergeHub.Source<int>(perProducerBufferSize: 16)
            .PreMaterialize(_mat);

        var (broadcastHubSource, broadcastHubSink) = BroadcastHub.Sink<int>(bufferSize: 2)
            .PreMaterialize(_mat);

        mergeHubSource
            .SelectAsyncUnordered(2, async x =>
            {
                await Task.Delay(10);
                return x;
            })
            .Buffer(32, OverflowStrategy.Backpressure)
            .To(broadcastHubSink)
            .Run(_mat);

        broadcastHubSource
            .RunWith(Sink.ForEach<int>(x => received.Add(x)), _mat);

        var sinkRef = await StreamRefs.SinkRef<int>()
            .To(mergeHubSink)
            .Run(_mat);

        var queue = Source.Queue<int>(4, OverflowStrategy.Backpressure)
            .To(sinkRef.Sink)
            .Run(_mat);

        var result = await queue.OfferAsync(42);

        Assert.Equal(QueueOfferResult.Enqueued.Instance, result);
    }

    [Fact(Timeout = 5000)]
    public async Task OfferAsync_completes_with_no_broadcasthub_consumers()
    {
        var (mergeHubSink, mergeHubSource) = MergeHub.Source<int>(perProducerBufferSize: 16)
            .PreMaterialize(_mat);

        var (_, broadcastHubSink) = BroadcastHub.Sink<int>(bufferSize: 2)
            .PreMaterialize(_mat);

        mergeHubSource
            .Buffer(32, OverflowStrategy.Backpressure)
            .To(broadcastHubSink)
            .Run(_mat);

        var sinkRef = await StreamRefs.SinkRef<int>()
            .To(mergeHubSink)
            .Run(_mat);

        var queue = Source.Queue<int>(4, OverflowStrategy.Backpressure)
            .To(sinkRef.Sink)
            .Run(_mat);

        var result = await queue.OfferAsync(42);

        Assert.Equal(QueueOfferResult.Enqueued.Instance, result);
    }

    [Fact(Timeout = 5000)]
    public async Task OfferAsync_completes_with_ask_consumer_and_no_responder()
    {
        var blackhole = _system.ActorOf(Props.Create(() => new BlackholeActor()));

        var (mergeHubSink, mergeHubSource) = MergeHub.Source<int>(perProducerBufferSize: 16)
            .PreMaterialize(_mat);

        var (broadcastHubSource, broadcastHubSink) = BroadcastHub.Sink<int>(bufferSize: 2)
            .PreMaterialize(_mat);

        mergeHubSource
            .SelectAsyncUnordered(2, async x =>
            {
                await Task.Delay(10);
                return x;
            })
            .Buffer(32, OverflowStrategy.Backpressure)
            .To(broadcastHubSink)
            .Run(_mat);

        broadcastHubSource
            .Ask<string>(blackhole, TimeSpan.FromSeconds(5))
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(
                _ => Akka.Streams.Supervision.Directive.Resume))
            .To(Sink.Ignore<string>())
            .Run(_mat);

        var sinkRef = await StreamRefs.SinkRef<int>()
            .To(mergeHubSink)
            .Run(_mat);

        var queue = Source.Queue<int>(4, OverflowStrategy.Backpressure)
            .To(sinkRef.Sink)
            .Run(_mat);

        var result = await queue.OfferAsync(42);

        Assert.Equal(QueueOfferResult.Enqueued.Instance, result);
    }

    [Fact(Timeout = 5000)]
    public async Task OfferAsync_completes_when_sinkref_is_prewarmed()
    {
        var (mergeHubSink, mergeHubSource) = MergeHub.Source<int>(perProducerBufferSize: 16)
            .PreMaterialize(_mat);

        var (broadcastHubSource, broadcastHubSink) = BroadcastHub.Sink<int>(bufferSize: 2)
            .PreMaterialize(_mat);

        mergeHubSource
            .SelectAsyncUnordered(2, async x =>
            {
                await Task.Delay(10);
                return x;
            })
            .Buffer(32, OverflowStrategy.Backpressure)
            .To(broadcastHubSink)
            .Run(_mat);

        broadcastHubSource
            .RunWith(Sink.ForEach<int>(_ => { }), _mat);

        var sinkRef = await StreamRefs.SinkRef<int>()
            .To(mergeHubSink)
            .Run(_mat);

        await Task.Delay(200);

        var queue = Source.Queue<int>(4, OverflowStrategy.Backpressure)
            .To(sinkRef.Sink)
            .Run(_mat);

        var result = await queue.OfferAsync(42);

        Assert.Equal(QueueOfferResult.Enqueued.Instance, result);
    }

    [Fact(Timeout = 5000)]
    public async Task OfferAsync_completes_when_sinkref_used_immediately()
    {
        var (mergeHubSink, mergeHubSource) = MergeHub.Source<int>(perProducerBufferSize: 16)
            .PreMaterialize(_mat);

        var (broadcastHubSource, broadcastHubSink) = BroadcastHub.Sink<int>(bufferSize: 2)
            .PreMaterialize(_mat);

        mergeHubSource
            .SelectAsyncUnordered(2, async x =>
            {
                await Task.Delay(10);
                return x;
            })
            .Buffer(32, OverflowStrategy.Backpressure)
            .To(broadcastHubSink)
            .Run(_mat);

        broadcastHubSource
            .RunWith(Sink.ForEach<int>(_ => { }), _mat);

        var sinkRef = await StreamRefs.SinkRef<int>()
            .To(mergeHubSink)
            .Run(_mat);

        var queue = Source.Queue<int>(4, OverflowStrategy.Backpressure)
            .To(sinkRef.Sink)
            .Run(_mat);

        var result = await queue.OfferAsync(42);

        Assert.Equal(QueueOfferResult.Enqueued.Instance, result);
    }

    [Fact(Timeout = 5000)]
    public async Task OfferAsync_completes_across_actor_boundary_with_full_pipeline()
    {
        var received = new TaskCompletionSource<int>();

        var pipeline = _system.ActorOf(Props.Create(() =>
            new FakePipelineWithFullGraph(_mat, received)));

        var sinkRefResponse = await pipeline.Ask<PipelineSinkResponse>(
            new RequestPipelineSink(), TimeSpan.FromSeconds(2));

        var queue = Source.Queue<int>(4, OverflowStrategy.Backpressure)
            .To(sinkRefResponse.SinkRef.Sink)
            .Run(_mat);

        var result = await queue.OfferAsync(42);

        Assert.Equal(QueueOfferResult.Enqueued.Instance, result);

        var value = await received.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(42, value);
    }

    [Fact(Timeout = 5000)]
    public async Task OfferAsync_completes_across_actor_boundary_with_prewarmed_sinkref()
    {
        var received = new TaskCompletionSource<int>();

        var pipeline = _system.ActorOf(Props.Create(() =>
            new PreWarmingPipelineActor(_mat, received)));

        var sinkRefResponse = await pipeline.Ask<PipelineSinkResponse>(
            new RequestPipelineSink(), TimeSpan.FromSeconds(2));

        var queue = Source.Queue<int>(4, OverflowStrategy.Backpressure)
            .To(sinkRefResponse.SinkRef.Sink)
            .Run(_mat);

        var result = await queue.OfferAsync(42);

        Assert.Equal(QueueOfferResult.Enqueued.Instance, result);

        var value = await received.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(42, value);
    }

    public sealed record RequestPipelineSink;
    public sealed record PipelineSinkResponse(ISinkRef<int> SinkRef);

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

            Receive<RequestPipelineSink>(_ =>
            {
                var sinkRef = StreamRefs.SinkRef<int>()
                    .To(mergeHubSink)
                    .Run(mat);
                sinkRef.PipeTo(Sender, Self,
                    sr => new PipelineSinkResponse(sr),
                    ex => null!);
            });
        }
    }

    private sealed class PreWarmingPipelineActor : ReceiveActor, IWithStash
    {
        private ISinkRef<int>? _warmSinkRef;
        private Sink<int, NotUsed> _mergeHubSink;
        private IMaterializer _mat;

        public IStash Stash { get; set; } = null!;

        private sealed record SinkRefWarmed(ISinkRef<int> SinkRef);

        public PreWarmingPipelineActor(IMaterializer mat, TaskCompletionSource<int> received)
        {
            _mat = mat;

            var (mergeHubSink, mergeHubSource) = MergeHub.Source<int>(perProducerBufferSize: 16)
                .PreMaterialize(mat);
            _mergeHubSink = mergeHubSink;

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

            PreWarmSinkRef();

            Receive<SinkRefWarmed>(msg =>
            {
                _warmSinkRef = msg.SinkRef;
                Stash.UnstashAll();
            });
            Receive<RequestPipelineSink>(_ =>
            {
                if (_warmSinkRef is not null)
                {
                    Sender.Tell(new PipelineSinkResponse(_warmSinkRef));
                    _warmSinkRef = null;
                    PreWarmSinkRef();
                }
                else
                {
                    Stash.Stash();
                }
            });
        }

        private void PreWarmSinkRef()
        {
            StreamRefs.SinkRef<int>()
                .To(_mergeHubSink)
                .Run(_mat)
                .PipeTo(Self,
                    success: sr => new SinkRefWarmed(sr),
                    failure: _ => null!);
        }
    }

    [Fact(Timeout = 5000)]
    public async Task OfferAsync_completes_with_real_budget_throttle()
    {
        var received = new TaskCompletionSource<int>();
        var gate = new AlwaysAllowGate();

        var (mergeHubSink, mergeHubSource) = MergeHub.Source<int>(perProducerBufferSize: 16)
            .PreMaterialize(_mat);

        var (broadcastHubSource, broadcastHubSink) = BroadcastHub.Sink<int>(bufferSize: 2)
            .PreMaterialize(_mat);

        mergeHubSource
            .Via(new BudgetThrottleStage<int>(gate))
            .SelectAsyncUnordered(2, x => Task.FromResult(x))
            .Buffer(32, OverflowStrategy.Backpressure)
            .To(broadcastHubSink)
            .Run(_mat);

        broadcastHubSource
            .RunWith(Sink.ForEach<int>(x => received.TrySetResult(x)), _mat);

        var sinkRef = await StreamRefs.SinkRef<int>()
            .To(mergeHubSink)
            .Run(_mat);

        var queue = Source.Queue<int>(4, OverflowStrategy.Backpressure)
            .To(sinkRef.Sink)
            .Run(_mat);

        var result = await queue.OfferAsync(42);
        Assert.Equal(QueueOfferResult.Enqueued.Instance, result);

        var value = await received.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(42, value);
    }

    [Fact(Timeout = 5000)]
    public async Task OfferAsync_completes_across_actors_with_budget_throttle()
    {
        var received = new TaskCompletionSource<int>();
        var gate = new AlwaysAllowGate();

        var pipeline = _system.ActorOf(Props.Create(() =>
            new ThrottledPipelineActor(_mat, received, gate)));

        var sinkRefResponse = await pipeline.Ask<PipelineSinkResponse>(
            new RequestPipelineSink(), TimeSpan.FromSeconds(2));

        var queue = Source.Queue<int>(4, OverflowStrategy.Backpressure)
            .To(sinkRefResponse.SinkRef.Sink)
            .Run(_mat);

        var result = await queue.OfferAsync(42);

        Assert.Equal(QueueOfferResult.Enqueued.Instance, result);

        var value = await received.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(42, value);
    }

    private sealed class ThrottledPipelineActor : ReceiveActor, IWithStash
    {
        private ISinkRef<int>? _warmSinkRef;
        private readonly Sink<int, NotUsed> _mergeHubSink;
        private readonly IMaterializer _mat;

        public IStash Stash { get; set; } = null!;

        private sealed record SinkRefWarmed(ISinkRef<int> SinkRef);

        public ThrottledPipelineActor(IMaterializer mat, TaskCompletionSource<int> received, IBudgetGate<int> gate)
        {
            _mat = mat;

            var (mergeHubSink, mergeHubSource) = MergeHub.Source<int>(perProducerBufferSize: 16)
                .PreMaterialize(mat);
            _mergeHubSink = mergeHubSink;

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

            PreWarmSinkRef();

            Receive<SinkRefWarmed>(msg =>
            {
                _warmSinkRef = msg.SinkRef;
                Stash.UnstashAll();
            });
            Receive<RequestPipelineSink>(_ =>
            {
                if (_warmSinkRef is not null)
                {
                    Sender.Tell(new PipelineSinkResponse(_warmSinkRef));
                    _warmSinkRef = null;
                    PreWarmSinkRef();
                }
                else
                {
                    Stash.Stash();
                }
            });
        }

        private void PreWarmSinkRef()
        {
            StreamRefs.SinkRef<int>()
                .To(_mergeHubSink)
                .Run(_mat)
                .PipeTo(Self,
                    success: sr => new SinkRefWarmed(sr),
                    failure: _ => null!);
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
