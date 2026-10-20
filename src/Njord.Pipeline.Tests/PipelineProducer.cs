using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Core.Actors;
using Njord.Core.Domain;
using Njord.Tests.Shared;

namespace Njord.Pipeline.Tests;

// A test producer: asks the pipeline for a SinkRef and runs a backpressured queue into it.
public static class PipelineProducer
{
    private static long NextRequestId;

    public static async Task<ISourceQueueWithComplete<DomainDataReceived>> AttachAsync(
        IActorRef pipeline, ActorSystem system, CancellationToken ct)
    {
        var requestId = Interlocked.Increment(ref NextRequestId);
        var response = await pipeline.Ask<RequestForecastSinkResponse>(
            new RequestForecastSink(requestId), TestTimeouts.RealTimeWaitMax, ct);
        var completed = response as RequestForecastSinkCompleted
            ?? throw new InvalidOperationException($"Pipeline answered {response} instead of a SinkRef");
        if (completed.RequestId != requestId)
        {
            throw new InvalidOperationException($"SinkRef reply carries request id {completed.RequestId}, expected {requestId}");
        }

        return Source.Queue<DomainDataReceived>(16, OverflowStrategy.Backpressure)
            .To(completed.SinkRef.Sink)
            .Run(system.Materializer());
    }

    public static async Task<IActorRef> AttachConsumerAsync(IActorRef pipeline, ActorSystem system, IActorRef probe, CancellationToken ct)
    {
        var output = await pipeline.Ask<RequestPipelineOutputResponse>(
            new RequestPipelineOutput(Interlocked.Increment(ref NextRequestId)), TestTimeouts.RealTimeWaitMax, ct);
        var completed = output as RequestPipelineOutputCompleted
            ?? throw new InvalidOperationException($"Pipeline answered {output} instead of a SourceRef");
        completed.SourceRef.Source
            .RunWith(Sink.ActorRef<Njord.Messages.Pipeline.PipelineEvent>(probe, ConsumerCompleted.Instance, ex => new ConsumerFailed(ex)),
                system.Materializer());
        return probe;
    }
}

public sealed record ConsumerCompleted
{
    public static readonly ConsumerCompleted Instance = new();
}

public sealed record ConsumerFailed(Exception Cause);
