using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;

namespace Njord.Pipeline.Tests.Helpers;

internal sealed class ProductionLikePipelineActor : ReceiveActor, IWithStash
{
    public IStash Stash { get; set; } = null!;

    private sealed record PipelineReady;

    public ProductionLikePipelineActor(TaskCompletionSource<int> offered)
    {
        var mat = Context.Materializer();
        var blackhole = Context.ActorOf(Props.Create(() => new BlackholeActor()));

        var (mergeHubSink, mergeHubSource) = MergeHub.Source<int>(perProducerBufferSize: 16)
            .PreMaterialize(mat);

        var (broadcastHubSource, broadcastHubSink) = BroadcastHub.Sink<int>(bufferSize: 2)
            .PreMaterialize(mat);

        mergeHubSource
            .SelectAsyncUnordered(2, x => Task.FromResult(x))
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(
                _ => Akka.Streams.Supervision.Directive.Resume))
            .Buffer(32, OverflowStrategy.Backpressure)
            .To(broadcastHubSink)
            .Run(mat);

        broadcastHubSource
            .RunWith(Sink.ForEach<int>(x => offered.TrySetResult(x)), mat);

        Receive<RequestPipelineSink>(_ =>
        {
            StreamRefs.SinkRef<int>()
                .To(mergeHubSink)
                .Run(mat)
                .PipeTo(Sender, Self,
                    sr => new PipelineSinkResponse(sr),
                    _ => null!);
        });
        Receive<RequestPipelineSource>(_ =>
        {
            broadcastHubSource
                .RunWith(StreamRefs.SourceRef<int>(), mat)
                .PipeTo(Sender, Self,
                    sr => new PipelineSourceResponse(sr),
                    _ => null!);
        });
    }
}

internal sealed record RequestPipelineSink;
internal sealed record PipelineSinkResponse(ISinkRef<int> SinkRef);
internal sealed record RequestPipelineSource;
internal sealed record PipelineSourceResponse(ISourceRef<int> SourceRef);
