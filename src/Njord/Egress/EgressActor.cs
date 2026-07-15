using Akka;
using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;

namespace Njord.Egress;

public sealed class EgressActor : ReceiveActor
{
    private readonly IMaterializer _mat;
    private Sink<EgressEvent, NotUsed>? _mergeHubSink;
    private Source<EgressEvent, NotUsed>? _broadcastHubSource;

    public EgressActor()
    {
        _mat = Context.Materializer();

        Receive<RequestEgressSink>(_ =>
        {
            if (_mergeHubSink is null)
            {
                return;
            }

            var sender = Sender;
            StreamRefs.SinkRef<EgressEvent>()
                .To(_mergeHubSink)
                .Run(_mat)
                .PipeTo(sender, Self,
                    sr => new EgressSinkResponse(sr),
                    ex => new Status.Failure(ex));
        });

        Receive<RequestEgressSource>(_ =>
        {
            if (_broadcastHubSource is null)
            {
                return;
            }

            var sender = Sender;
            _broadcastHubSource
                .RunWith(StreamRefs.SourceRef<EgressEvent>(), _mat)
                .PipeTo(sender, Self,
                    sr => new EgressSourceResponse(sr),
                    ex => new Status.Failure(ex));
        });
    }

    protected override void PreStart()
    {
        (_broadcastHubSource, var broadcastHubSink) = BroadcastHub.Sink<EgressEvent>(bufferSize: 64)
            .PreMaterialize(_mat);

        (_mergeHubSink, var mergeHubSource) = MergeHub.Source<EgressEvent>(perProducerBufferSize: 8)
            .PreMaterialize(_mat);

        mergeHubSource
            .To(broadcastHubSink)
            .Run(_mat);
    }
}
