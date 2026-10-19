using Akka.Actor;
using Akka.Hosting;
using Akka.Persistence;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Core;
using Servus.Akka;

namespace Njord.Pipeline.Tests;

internal sealed class PersistentConsumerActor : ReceivePersistentActor
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
        var pipeline = Context.GetActor<IForecastPipeline>();
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
        Command<DoOffer>(_ => Stash.Stash());
    }

    private void TryConnect()
    {
        if (_queue is null || !_sourceReceived)
        {
            return;
        }

        Self.Tell(new DoOffer());
        Become(Connecting);
    }

    private sealed record DoOffer;

    private void Connecting()
    {
        Command<DoOffer>(_ =>
        {
            Assert.NotNull(_queue);
            _queue.OfferAsync(99).PipeTo(Self,
                success: _ => new ConnectionEstablished(),
                failure: ex => new OfferFailed(ex));
            Become(WaitingForConnection);
        });
        Command<DoOffer>(_ => Stash.Stash());
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
        Command<DoOffer>(_ => Stash.Stash());
    }

    private void Ready()
    {
        CommandAny(_ => { });
    }
}
