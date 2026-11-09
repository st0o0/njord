using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Google.Protobuf.WellKnownTypes;
using Njord.Core;
using Njord.Grpc.Mapping.V3;
using Njord.Messages.Fetch;
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

    private IActorRef _hubInput = ActorRefs.Nobody;
    private Source<PipelineEvent, NotUsed> _clientHubSource = Source.Empty<PipelineEvent>();

    private IActorRef _entityHubInput = ActorRefs.Nobody;
    private Source<V3.EntityEvent, NotUsed> _entityHubSource = Source.Empty<V3.EntityEvent>();

    private GrpcConsumerState _state = GrpcConsumerState.Empty;

    private sealed record StreamCompleted;

    internal sealed record CheckAvailability;

    private sealed record AvailabilityTick;

    public ITimerScheduler Timers { get; set; } = null!;

    public GrpcConsumer(EntityIdLookup lookup, TimeProvider timeProvider)
    {
        _lookup = lookup;
        _timeProvider = timeProvider;

        Receive<QueryDomainData>(HandleQueryDomainData);
        Receive<QueryLatestAnalysis>(HandleQueryLatestAnalysis);
        Receive<RequestGrpcStream>(HandleStreamRequest);
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

        var (hubInput, hubSource) = Source.ActorRef<PipelineEvent>(256, OverflowStrategy.DropHead)
            .ToMaterialized(BroadcastHub.Sink<PipelineEvent>(bufferSize: 2), Keep.Both)
            .Run(_mat);

        hubSource.RunWith(Sink.Ignore<PipelineEvent>(), _mat);

        _hubInput = hubInput;
        _clientHubSource = hubSource;

        var (entityInput, entitySource) = Source.ActorRef<V3.EntityEvent>(256, OverflowStrategy.DropHead)
            .ToMaterialized(BroadcastHub.Sink<V3.EntityEvent>(bufferSize: 2), Keep.Both)
            .Run(_mat);

        entitySource.RunWith(Sink.Ignore<V3.EntityEvent>(), _mat);

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
        _state = _state.Apply(evt);
        _hubInput.Tell(evt);

        if (evt is FeatureResult result && _lookup.Find(result) is { } entityId)
        {
            var now = _timeProvider.GetUtcNow();
            _state = _state.ApplyEntity(entityId, result, now);
            _entityHubInput.Tell(new V3.EntityEvent { StateUpdate = result.ToEntityStateUpdate(entityId, now) });
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
            _entityHubInput.Tell(new V3.EntityEvent
            {
                Unavailable = new V3.EntityUnavailable { EntityId = id, Since = Timestamp.FromDateTimeOffset(since) },
            });
        }

        foreach (var id in available)
        {
            _entityHubInput.Tell(new V3.EntityEvent
            {
                Available = new V3.EntityAvailable { EntityId = id, RecoveredAt = Timestamp.FromDateTimeOffset(now) },
            });
        }
    }

    private void HandleQueryDomainData(QueryDomainData msg)
    {
        Sender.Tell(_state.GetDomainData(msg.Location, msg.Domain, msg.ModelId) is { } latest
            ? new DomainDataResult(latest)
            : new DomainDataNotAvailable(msg.Location, msg.Domain, msg.ModelId));
    }

    private void HandleQueryLatestAnalysis(QueryLatestAnalysis msg)
    {
        var results = _state.GetLatestResults(msg.Location);
        Sender.Tell(results.Count == 0
            ? new LatestAnalysisNotAvailable(msg.Location)
            : new LatestAnalysisResult(msg.Location, results));
    }

    private void HandleStreamRequest(RequestGrpcStream msg)
    {
        var snapshot = _state.DomainData.Values
            .Concat<PipelineEvent>(_state.Results.Values);

        Source.From(snapshot)
            .Concat(_clientHubSource)
            .KeepAlive<PipelineEvent, PipelineEvent, NotUsed>(_keepAliveInterval, () => Heartbeat.Instance)
            .Buffer(StreamClientBufferSize, OverflowStrategy.Backpressure)
            .IdleTimeout(_clientIdleTimeout)
            .Where(e => e is not Heartbeat)
            .RunWith(StreamRefs.SourceRef<PipelineEvent>(), _mat)
            .PipeTo(Sender, Self,
                sr => new RequestGrpcStreamCompleted(msg.RequestId, sr),
                ex => new RequestGrpcStreamFailed(msg.RequestId, ex));
    }

    private void HandleEntityStreamRequest(RequestEntityStream msg)
    {
        var snapshot = new V3.EntitySnapshot();
        snapshot.States.AddRange(_state.EntityStates.Values
            .OrderBy(e => e.EntityId, StringComparer.Ordinal)
            .Select(e => e.Result.ToEntityStateUpdate(e.EntityId, e.LastUpdated)));

        var initial = new List<V3.EntityEvent> { new() { Snapshot = snapshot } };
        initial.AddRange(_state.StaleEntities.Order(StringComparer.Ordinal).Select(id => new V3.EntityEvent
        {
            Unavailable = new V3.EntityUnavailable
            {
                EntityId = id,
                Since = Timestamp.FromDateTimeOffset(_state.EntityStates[id].LastUpdated),
            },
        }));

        Source.From(initial)
            .Concat(_entityHubSource)
            .KeepAlive<V3.EntityEvent, V3.EntityEvent, NotUsed>(_keepAliveInterval, () => new V3.EntityEvent())
            .Buffer(StreamClientBufferSize, OverflowStrategy.Backpressure)
            .IdleTimeout(_clientIdleTimeout)
            .Where(e => e.EventCase != V3.EntityEvent.EventOneofCase.None)
            .RunWith(StreamRefs.SourceRef<V3.EntityEvent>(), _mat)
            .PipeTo(Sender, Self,
                sr => new RequestEntityStreamCompleted(msg.RequestId, sr),
                ex => new RequestEntityStreamFailed(msg.RequestId, ex));
    }
}
