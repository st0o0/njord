using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Egress;
using Njord.Pipeline;
using Servus.Akka;

namespace Njord.Grpc;

public sealed class GrpcSnapshotConsumerActor : ReceiveActor, IWithStash
{
    private readonly ILogger<GrpcSnapshotConsumerActor> _logger;
    private IMaterializer? _mat;
    private ISourceRef<EgressEvent>? _pendingSourceRef;

    public IStash Stash { get; set; } = null!;

    private sealed record EgressResolved(IActorRef Ref);
    private sealed record SnapshotActorsResolved(IActorRef Forecast, IActorRef Enrichment);

    public GrpcSnapshotConsumerActor(ILogger<GrpcSnapshotConsumerActor> logger)
    {
        _logger = logger;
        WaitingForSource();
    }

    protected override void PreStart()
    {
        _mat = Context.Materializer();
        RequestEgressSource();
    }

    private void RequestEgressSource()
    {
        Context.GetActorAsync<EgressActor>().PipeTo(Self, success: r => new EgressResolved(r));
    }

    private void WaitingForSource()
    {
        Receive<EgressResolved>(msg =>
        {
            Context.Watch(msg.Ref);
            msg.Ref.Tell(new RequestEgressSource());
        });
        Receive<EgressSourceResponse>(response =>
        {
            _pendingSourceRef = response.SourceRef;

            var forecastTask = Context.GetActorAsync<ForecastSnapshotActor>();
            var enrichmentTask = Context.GetActorAsync<EnrichmentSnapshotActor>();
            Task.WhenAll(forecastTask, enrichmentTask)
                .PipeTo(Self, success: _ => new SnapshotActorsResolved(forecastTask.Result, enrichmentTask.Result));

            Become(WaitingForSnapshotActors);
        });
        Receive<Terminated>(OnTerminated);
        ReceiveAny(_ => Stash.Stash());
    }

    private void WaitingForSnapshotActors()
    {
        Receive<SnapshotActorsResolved>(msg =>
        {
            MaterializeGraph(_pendingSourceRef!, msg.Forecast, msg.Enrichment);
            _pendingSourceRef = null;
            _logger.LogInformation("gRPC snapshot consumer materialized — capturing forecasts and enrichments");
            Become(Ready);
            Stash.UnstashAll();
        });
        Receive<Terminated>(OnTerminated);
        ReceiveAny(_ => Stash.Stash());
    }

    private void Ready()
    {
        Receive<Terminated>(OnTerminated);
    }

    private void OnTerminated(Terminated msg)
    {
        _logger.LogWarning("Watched actor {Actor} terminated — re-requesting source", msg.ActorRef.Path.Name);
        _pendingSourceRef = null;
        RequestEgressSource();
        Become(WaitingForSource);
    }

    private void MaterializeGraph(ISourceRef<EgressEvent> sourceRef, IActorRef forecastActor, IActorRef enrichmentActor)
    {
        sourceRef.Source
            .SelectAsync(1, async update => update switch
            {
                EgressEvent.PerModelUpdate pmu =>
                    await forecastActor.Ask<Ack>(
                        new UpdateForecast(pmu.Location, pmu.Model, pmu.Forecast))
                    is var _ ? update : update,

                EgressEvent.EnrichmentUpdate eu =>
                    await enrichmentActor.Ask<Ack>(
                        new UpdateEnrichment(eu.Location, eu.TypeName, eu.Result))
                    is var _ ? update : update,

                _ => update,
            })
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(StreamSupervision.LoggingDecider(_logger)))
            .To(Sink.Ignore<EgressEvent>())
            .Run(_mat!);
    }
}
