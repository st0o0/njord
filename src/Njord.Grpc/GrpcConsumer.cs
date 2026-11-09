using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Google.Protobuf.WellKnownTypes;
using Njord.Core;
using Njord.Grpc.Mapping;
using Njord.Messages.Results;
using Njord.Messages.Stream;
using Terp.Akka;

namespace Njord.Grpc;

public sealed class GrpcConsumer : ReceiveActor, IWithTimers
{
    private const int StreamClientBufferSize = 256;
    private static readonly TimeSpan _keepAliveInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _clientIdleTimeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan _availabilityCheckInterval = TimeSpan.FromSeconds(60);

    internal static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(150);

    private readonly ILoggingAdapter _log = Context.GetLogger();
    private readonly EntityIdLookup _lookup;
    private readonly TimeProvider _timeProvider;
    private IMaterializer _mat = null!;

    private IActorRef _entityHubInput = ActorRefs.Nobody;
    private Source<Proto.EntityEvent, NotUsed> _entityHubSource = Source.Empty<Proto.EntityEvent>();

    private GrpcConsumerState _state = GrpcConsumerState.Empty;

    private sealed record StreamCompleted;

    internal sealed record CheckAvailability;

    private sealed record AvailabilityTick;

    public ITimerScheduler Timers { get; set; } = null!;

    public GrpcConsumer(EntityIdLookup lookup, TimeProvider timeProvider)
    {
        _lookup = lookup;
        _timeProvider = timeProvider;

        Receive<RequestEntityStream>(HandleEntityStreamRequest);
        Receive<PipelineEvent>(OnPipelineEvent);
        Receive<StreamCompleted>(_ => ReconnectToPipeline());
        Receive<AvailabilityTick>(_ => OnCheckAvailability());
        Receive<CheckAvailability>(_ => OnCheckAvailability());
        Receive<RequestPipelineOutputCompleted>(OnSourceRef);
        Receive<RequestPipelineOutputFailed>(msg => _log.Warning(msg.Cause, "Pipeline could not create a SourceRef"));
    }

    protected override void PreStart()
    {
        _mat = Context.Materializer();

        var (entityInput, entitySource) = Source.ActorRef<Proto.EntityEvent>(256, OverflowStrategy.DropHead)
            .ToMaterialized(BroadcastHub.Sink<Proto.EntityEvent>(bufferSize: 2), Keep.Both)
            .Run(_mat);

        entitySource.RunWith(Sink.Ignore<Proto.EntityEvent>(), _mat);

        _entityHubInput = entityInput;
        _entityHubSource = entitySource;

        Timers.StartPeriodicTimer("availability", new AvailabilityTick(), _availabilityCheckInterval);

        var pipeline = Context.GetActor<IForecastPipeline>();
        pipeline.Tell(new RequestPipelineOutput(0));
    }

    private void ReconnectToPipeline()
    {
        _log.Info("Pipeline SourceRef stream completed, reconnecting");
        var pipeline = Context.GetActor<IForecastPipeline>();
        pipeline.Tell(new RequestPipelineOutput(0));
    }

    private void OnSourceRef(RequestPipelineOutputCompleted msg)
    {
        _log.Info("Pipeline SourceRef received, gRPC consumer attached");
        msg.SourceRef.Source
            .WatchTermination(Keep.Right)
            .To(Sink.ActorRef<PipelineEvent>(Self, new StreamCompleted(), _ => new StreamCompleted()))
            .Run(_mat);
    }

    private void OnPipelineEvent(PipelineEvent evt)
    {
        if (evt is FeatureResult result && _lookup.Find(result) is { } entityId)
        {
            var now = _timeProvider.GetUtcNow();
            _state = _state.ApplyEntity(entityId, result, now);
            _entityHubInput.Tell(new Proto.EntityEvent { StateUpdate = result.ToEntityStateUpdate(entityId, now) });
        }
    }

    private void OnCheckAvailability()
    {
        var now = _timeProvider.GetUtcNow();
        var (next, unavailable, available) = _state.CheckAvailability(now, StaleThreshold);
        _state = next;

        foreach (var id in unavailable)
        {
            var since = _state.EntityStates[id].LastUpdated;
            _entityHubInput.Tell(new Proto.EntityEvent
            {
                Unavailable = new Proto.EntityUnavailable { EntityId = id, Since = Timestamp.FromDateTimeOffset(since) },
            });
        }

        foreach (var id in available)
        {
            _entityHubInput.Tell(new Proto.EntityEvent
            {
                Available = new Proto.EntityAvailable { EntityId = id, RecoveredAt = Timestamp.FromDateTimeOffset(now) },
            });
        }
    }

    private void HandleEntityStreamRequest(RequestEntityStream msg)
    {
        var snapshot = new Proto.EntitySnapshot();
        snapshot.States.AddRange(_state.EntityStates.Values
            .OrderBy(e => e.EntityId, StringComparer.Ordinal)
            .Select(e => e.Result.ToEntityStateUpdate(e.EntityId, e.LastUpdated)));

        var initial = new List<Proto.EntityEvent> { new() { Snapshot = snapshot } };
        initial.AddRange(_state.StaleEntities.Order(StringComparer.Ordinal).Select(id => new Proto.EntityEvent
        {
            Unavailable = new Proto.EntityUnavailable
            {
                EntityId = id,
                Since = Timestamp.FromDateTimeOffset(_state.EntityStates[id].LastUpdated),
            },
        }));

        Source.From(initial)
            .Concat(_entityHubSource)
            .KeepAlive<Proto.EntityEvent, Proto.EntityEvent, NotUsed>(_keepAliveInterval, () => new Proto.EntityEvent())
            .Buffer(StreamClientBufferSize, OverflowStrategy.Backpressure)
            .IdleTimeout(_clientIdleTimeout)
            .Where(e => e.EventCase != Proto.EntityEvent.EventOneofCase.None)
            .RunWith(StreamRefs.SourceRef<Proto.EntityEvent>(), _mat)
            .PipeTo(Sender, Self,
                sr => new RequestEntityStreamCompleted(msg.RequestId, sr),
                ex => new RequestEntityStreamFailed(msg.RequestId, ex));
    }
}
