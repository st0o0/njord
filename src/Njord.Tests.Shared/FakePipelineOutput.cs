using System.Threading.Channels;
using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Core.Actors;
using Njord.Messages.Pipeline;

namespace Njord.Tests.Shared;

// Stands in for ForecastPipeline's output: every RequestPipelineOutput gets its own queue-backed SourceRef, so a
// spec can fail or complete one subscription and wait, bounded, for the consumer to request the next one.
public sealed class FakePipelineOutput
{
    private readonly Channel<ISourceQueueWithComplete<PipelineEvent>> _subscriptions =
        Channel.CreateUnbounded<ISourceQueueWithComplete<PipelineEvent>>();

    private int _requests;

    public IActorRef Actor { get; }

    public int Requests => Volatile.Read(ref _requests);

    public FakePipelineOutput(ActorSystem system)
    {
        Actor = system.ActorOf(Props.Create(() => new Producer(this)), $"fake-pipeline-{Guid.NewGuid():N}");
    }

    // The queue behind the next SourceRef the consumer requests, in request order.
    public async Task<ISourceQueueWithComplete<PipelineEvent>> NextSubscriptionAsync(string description, CancellationToken ct)
    {
        try
        {
            return await _subscriptions.Reader.ReadAsync(ct).AsTask().WaitAsync(TestTimeouts.RealTimeWaitMax, ct);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException(
                $"Timed out after {TestTimeouts.RealTimeWaitMax} waiting for {description} ({Requests} SourceRef requests so far)");
        }
    }

    private sealed class Producer : ReceiveActor
    {
        public Producer(FakePipelineOutput owner)
        {
            var mat = Context.Materializer();

            Receive<RequestPipelineOutput>(msg =>
            {
                Interlocked.Increment(ref owner._requests);
                var (queue, source) = Source.Queue<PipelineEvent>(64, OverflowStrategy.Backpressure).PreMaterialize(mat);
                owner._subscriptions.Writer.TryWrite(queue);
                source.RunWith(StreamRefs.SourceRef<PipelineEvent>(), mat)
                    .PipeTo(Sender, Self,
                        sr => new RequestPipelineOutputCompleted(msg.RequestId, sr),
                        ex => new RequestPipelineOutputFailed(msg.RequestId, ex));
            });
        }
    }
}
