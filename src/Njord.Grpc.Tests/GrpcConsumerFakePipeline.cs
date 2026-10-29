using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Core.Actors;
using Njord.Core.Domain;
using Njord.Messages.Stream;

namespace Njord.Grpc.Tests;

public sealed record TestPipeline(
    ISourceQueueWithComplete<PipelineEvent> Queue,
    Source<PipelineEvent, Akka.NotUsed> HubSource);

// Mirrors ForecastPipeline's output: a BroadcastHub (buffer size 2) that every consumer reads from,
// so a stalled read model holds up the other consumers of the hub as it would in production.
internal sealed class FakePipeline : ReceiveActor
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
