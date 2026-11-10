using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Core;
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
    private readonly EntityRegistry _registry;
    private readonly TimeProvider _timeProvider;
    private IMaterializer _mat = null!;

    private IActorRef _entityHubInput = ActorRefs.Nobody;
    private Source<ConsumerEvent, NotUsed> _entityHubSource = Source.Empty<ConsumerEvent>();

    private GrpcConsumerState _state = GrpcConsumerState.Empty;

    private sealed record StreamCompleted;

    internal sealed record CheckAvailability;

    private sealed record AvailabilityTick;

    public ITimerScheduler Timers { get; set; } = null!;

    public GrpcConsumer(EntityRegistry registry, TimeProvider timeProvider)
    {
        _registry = registry;
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

        var (entityInput, entitySource) = Source.ActorRef<ConsumerEvent>(256, OverflowStrategy.DropHead)
            .ToMaterialized(BroadcastHub.Sink<ConsumerEvent>(bufferSize: 2), Keep.Both)
            .Run(_mat);

        entitySource.RunWith(Sink.Ignore<ConsumerEvent>(), _mat);

        _entityHubInput = entityInput;
        _entityHubSource = entitySource;

        SeedDefaultState();

        Timers.StartPeriodicTimer("availability", new AvailabilityTick(), _availabilityCheckInterval);

        var pipeline = Context.GetActor<IForecastPipeline>();
        pipeline.Tell(new RequestPipelineOutput(0));
    }

    private void SeedDefaultState()
    {
        var startTime = _timeProvider.GetUtcNow();
        foreach (var entry in _registry.All)
        {
            if (DefaultResultFactory.Create(entry.Descriptor, entry.Location) is { } defaultResult)
            {
                _state = _state.ApplyEntity(entry.EntityId, defaultResult, startTime);
            }
        }
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
        if (evt is FeatureResult result && _registry.Find(result) is { } entityId)
        {
            var now = _timeProvider.GetUtcNow();
            _state = _state.ApplyEntity(entityId, result, now);
            _entityHubInput.Tell(new EntityStateChanged(entityId, result, now));
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
            _entityHubInput.Tell(new EntityBecameUnavailable(id, since));
        }

        foreach (var id in available)
        {
            _entityHubInput.Tell(new EntityBecameAvailable(id, now));
        }
    }

    private void HandleEntityStreamRequest(RequestEntityStream msg)
    {
        var snapshot = new ConsumerSnapshot([.. _state.EntityStates.Values
            .OrderBy(e => e.EntityId, StringComparer.Ordinal)]);

        var initial = new List<ConsumerEvent> { snapshot };
        initial.AddRange(_state.StaleEntities.Order(StringComparer.Ordinal)
            .Select(id => (ConsumerEvent)new EntityBecameUnavailable(id, _state.EntityStates[id].LastUpdated)));

        Source.From(initial)
            .Concat(_entityHubSource)
            .KeepAlive<ConsumerEvent, ConsumerEvent, NotUsed>(_keepAliveInterval, () => new ConsumerKeepAlive())
            .Buffer(StreamClientBufferSize, OverflowStrategy.Backpressure)
            .IdleTimeout(_clientIdleTimeout)
            .Where(static e => e is not ConsumerKeepAlive)
            .RunWith(StreamRefs.SourceRef<ConsumerEvent>(), _mat)
            .PipeTo(Sender, Self,
                sr => new RequestEntityStreamCompleted(msg.RequestId, sr),
                ex => new RequestEntityStreamFailed(msg.RequestId, ex));
    }
}
