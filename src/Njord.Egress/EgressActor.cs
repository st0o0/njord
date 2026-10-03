using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Messages.Common;
using Njord.Messages.Egress;

namespace Njord.Egress;

public sealed class EgressActor : ReceiveActor
{
    private readonly IMaterializer _mat;
    private ILoggingAdapter _log = null!;
    private Sink<EgressEvent, NotUsed>? _mergeHubSink;
    private Source<EgressEvent, NotUsed>? _broadcastHubSource;
    private UniqueKillSwitch? _killSwitch;
    private Task? _completion;

    public EgressActor()
    {
        _mat = Context.Materializer();

        Receive<RequestEgressSink>(msg =>
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
                    sr => new EgressSinkResponse(msg.RequestId, sr),
                    ex => new EgressSinkFailed(msg.RequestId, ex));
        });

        Receive<StopStreams>(_ =>
        {
            _killSwitch?.Shutdown();
            (_completion ?? Task.CompletedTask)
                .PipeTo(Sender, Self,
                    success: () => new StreamsStopped(),
                    failure: ex => new StreamsStopFailed(ex));
        });

        Receive<RequestEgressSource>(msg =>
        {
            if (_broadcastHubSource is null)
            {
                return;
            }

            var sender = Sender;
            _broadcastHubSource
                .RunWith(StreamRefs.SourceRef<EgressEvent>(), _mat)
                .PipeTo(sender, Self,
                    sr => new EgressSourceResponse(msg.RequestId, sr),
                    ex => new EgressSourceFailed(msg.RequestId, ex));
        });
    }

    protected override void PreStart()
    {
        _log = Context.GetLogger();

        (_broadcastHubSource, var broadcastHubSink) = BroadcastHub.Sink<EgressEvent>(bufferSize: 4)
            .PreMaterialize(_mat);

        (_mergeHubSink, var mergeHubSource) = MergeHub.Source<EgressEvent>(perProducerBufferSize: 8)
            .PreMaterialize(_mat);

        (_killSwitch, _completion) = mergeHubSource
            .ViaMaterialized(KillSwitches.Single<EgressEvent>(), Keep.Right)
            .Log("egress-hub", e => e switch
            {
                EgressEvent.PerModelUpdate u => $"model {u.Location}/{u.Model.Id}",
                EgressEvent.EnrichmentUpdate u => $"enrich {u.Location}/{u.TypeName}",
                EgressEvent.CapabilityLearned c => $"cap {c.Location}/{c.Model.Id}",
                _ => "?",
            }, _log)
            .WatchTermination((killSwitch, completion) => (killSwitch, completion))
            .To(broadcastHubSink)
            .Run(_mat);
    }
}
