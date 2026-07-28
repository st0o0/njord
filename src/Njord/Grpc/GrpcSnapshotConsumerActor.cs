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

    public IStash Stash { get; set; } = null!;

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
        var egressActor = Context.GetActor<EgressActor>();
        Context.Watch(egressActor);
        egressActor.Tell(new RequestEgressSource());
    }

    private void WaitingForSource()
    {
        Receive<EgressSourceResponse>(response =>
        {
            MaterializeGraph(response.SourceRef);
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
        RequestEgressSource();
        Become(WaitingForSource);
    }

    private void MaterializeGraph(ISourceRef<EgressEvent> sourceRef)
    {
        var forecastActor = Context.GetActor<ForecastSnapshotActor>();
        var enrichmentActor = Context.GetActor<EnrichmentSnapshotActor>();

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
