using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Tests.Shared;

namespace Njord.Pipeline.Tests;

public sealed class SinkRefHubConsumerTests : Akka.Hosting.TestKit.TestKit
{
    protected IMaterializer Mat => Sys.Materializer();

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.AddTestPersistence();
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
                await Task.Yield();
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

        var result = await queue.OfferAsync(42, TestContext.Current.CancellationToken);

        Assert.Equal(QueueOfferResult.Enqueued.Instance, result);
    }

    [Fact(Timeout = 5000)]
    public async Task OfferAsync_completes_with_real_budget_throttle()
    {
        var received = new TaskCompletionSource<int>();

        var (mergeHubSink, mergeHubSource) = MergeHub.Source<int>(perProducerBufferSize: 16)
            .PreMaterialize(Mat);

        var (broadcastHubSource, broadcastHubSink) = BroadcastHub.Sink<int>(bufferSize: 2)
            .PreMaterialize(Mat);

        mergeHubSource
            .SelectAsyncUnordered(2, x => Task.FromResult(x))
            .Buffer(32, OverflowStrategy.Backpressure)
            .To(broadcastHubSink)
            .Run(Mat);

        _ = broadcastHubSource
            .RunWith(Sink.ForEach<int>(x => received.TrySetResult(x)), Mat);

        var sinkRef = await StreamRefs.SinkRef<int>()
            .To(mergeHubSink)
            .Run(Mat);

        var queue = Source.Queue<int>(4, OverflowStrategy.Backpressure)
            .To(sinkRef.Sink)
            .Run(Mat);

        var result = await queue.OfferAsync(42, TestContext.Current.CancellationToken);
        Assert.Equal(QueueOfferResult.Enqueued.Instance, result);

        var value = await received.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        Assert.Equal(42, value);
    }
}
