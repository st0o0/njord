using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;

namespace Njord.Pipeline.Tests;

internal sealed record RequestSinkRef;
internal sealed record SinkRefResponse(ISinkRef<int> SinkRef);

internal sealed class FakePipelineWithFullGraph : ReceiveActor
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
                await Task.Yield();
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

internal sealed class ThrottledPipelineActor : ReceiveActor
{
    public ThrottledPipelineActor(IMaterializer mat, TaskCompletionSource<int> received)
    {
        var (mergeHubSink, mergeHubSource) = MergeHub.Source<int>(perProducerBufferSize: 16)
            .PreMaterialize(mat);

        var (broadcastHubSource, broadcastHubSink) = BroadcastHub.Sink<int>(bufferSize: 2)
            .PreMaterialize(mat);

        mergeHubSource
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

internal sealed record RequestSourceRef;
internal sealed record SourceRefResponse;

internal sealed class BlackholeActor : ReceiveActor
{
    public BlackholeActor()
    {
        ReceiveAny(_ => { });
    }
}
