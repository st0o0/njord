using Akka.Actor;
using Akka.Persistence;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Core;
using Servus.Akka;

namespace Njord.Pipeline.Tests;

internal sealed class ProductionLikeSchedulerActor : ReceivePersistentActor
{
    public override string PersistenceId { get; }

    private IMaterializer _mat = null!;
    private ISourceQueueWithComplete<int>? _queue;
    private bool _sourceReceived;

    private sealed record ConnectionEstablished;
    private sealed record OfferFailed(Exception Error);
    private sealed record FailureConsumerDone;
    private sealed record FailureConsumerBroke(Exception Cause);
    private sealed record Evt(int Value);

    public ProductionLikeSchedulerActor(string persistenceId)
    {
        PersistenceId = persistenceId;

        Recover<Evt>(_ => { });
        Recover<SnapshotOffer>(_ => { });

        WaitingForRefs();
    }

    protected override void PreStart()
    {
        _mat = Context.Materializer();
        var pipeline = Context.GetActor<IForecastPipeline>();
        pipeline.Tell(new RequestPipelineSink());
        pipeline.Tell(new RequestPipelineSource());
    }

    private void WaitingForRefs()
    {
        Command<PipelineSinkResponse>(response =>
        {
            _queue = Source.Queue<int>(4, OverflowStrategy.Backpressure)
                .To(response.SinkRef.Sink)
                .Run(_mat);
            TryConnect();
        });
        Command<PipelineSourceResponse>(response =>
        {
            response.SourceRef.Source
                .Collect(x => x < 0, x => x)
                .To(Sink.ActorRef<int>(Self, new FailureConsumerDone(),
                    ex => new FailureConsumerBroke(ex)))
                .Run(_mat);

            _sourceReceived = true;
            TryConnect();
        });
        Command<SchedulePoll>(_ => Stash.Stash());
        Command<ConnectionEstablished>(_ => Stash.Stash());
    }

    private void TryConnect()
    {
        if (_queue is null || !_sourceReceived)
        {
            return;
        }

        Self.Tell(new SchedulePoll());
        Become(Connecting);
    }

    private sealed record SchedulePoll;

    private void Connecting()
    {
        Command<SchedulePoll>(_ =>
        {
            Assert.NotNull(_queue);
            _queue.OfferAsync(42).PipeTo(Self,
                success: _ => new ConnectionEstablished(),
                failure: ex => new OfferFailed(ex));
            Become(WaitingForConnection);
        });
        Command<ConnectionEstablished>(_ => Stash.Stash());
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
            Self.Tell(new SchedulePoll());
            Become(Connecting);
        });
        Command<SchedulePoll>(_ => Stash.Stash());
    }

    private void Ready()
    {
        Command<SchedulePoll>(_ => { });
    }
}
