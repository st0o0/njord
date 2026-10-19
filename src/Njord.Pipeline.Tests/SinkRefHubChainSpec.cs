using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Core;

namespace Njord.Pipeline.Tests;

public sealed class SinkRefHubChainSpec : SinkRefTestKit
{
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
                await Task.Yield();
                return x;
            })
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

        var result = await queue.OfferAsync(42, TestContext.Current.CancellationToken);

        Assert.Equal(QueueOfferResult.Enqueued.Instance, result);
    }
}
