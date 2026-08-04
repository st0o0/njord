using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Actors;
using Njord.Egress;
using Njord.Pipeline;
using Servus.Akka;

namespace Njord.Grpc;

public sealed class GrpcSnapshotConsumerActor : StreamConsumerActor
{
    private ISourceRef<EgressEvent>? _sourceRef;
    private IActorRef? _forecastActor;
    private IActorRef? _enrichmentActor;

    private sealed record EgressResolved(IActorRef Ref);
    private sealed record SnapshotActorsResolved(IActorRef Forecast, IActorRef Enrichment);

    protected override void ResolveDependencies()
    {
        Context.GetActorAsync<EgressActor>().PipeTo(Self, success: r => new EgressResolved(r));
    }

    protected override void ConfigureWaitingForRefs()
    {
        Receive<EgressResolved>(msg =>
        {
            if (IsDeadRef(msg.Ref))
            {
                ScheduleRetryResolve();
                return;
            }

            TrackDependency(msg.Ref);
            msg.Ref.Tell(new RequestEgressSource());
        });
        Receive<EgressSourceResponse>(response =>
        {
            _sourceRef = response.SourceRef;

            var forecastTask = Context.GetActorAsync<ForecastSnapshotActor>();
            var enrichmentTask = Context.GetActorAsync<EnrichmentSnapshotActor>();
            Task.WhenAll(forecastTask, enrichmentTask)
                .PipeTo(Self, success: _ => new SnapshotActorsResolved(forecastTask.Result, enrichmentTask.Result));
        });
        Receive<SnapshotActorsResolved>(msg =>
        {
            _forecastActor = msg.Forecast;
            _enrichmentActor = msg.Enrichment;
            TryTransition();
        });
    }

    protected override bool AllRefsReady() =>
        _sourceRef is not null && _forecastActor is not null && _enrichmentActor is not null;

    protected override void MaterializeGraph(SharedKillSwitch killSwitch)
    {
        var log = Context.GetLogger();
        var forecastActor = _forecastActor!;
        var enrichmentActor = _enrichmentActor!;

        _sourceRef!.Source
            .Via(killSwitch.Flow<EgressEvent>())
            .Log("grpc-snapshot-in", e => e switch
            {
                EgressEvent.PerModelUpdate u => $"model {u.Location}/{u.Model.Id}",
                EgressEvent.EnrichmentUpdate u => $"enrich {u.Location}/{u.TypeName}",
                _ => "?",
            }, log)
            .SelectAsync(1, async update =>
            {
                switch (update)
                {
                    case EgressEvent.PerModelUpdate pmu:
                        await forecastActor.Ask<Ack>(
                            new UpdateForecast(pmu.Location, pmu.Model, pmu.Forecast));
                        break;

                    case EgressEvent.EnrichmentUpdate eu:
                        await enrichmentActor.Ask<Ack>(
                            new UpdateEnrichment(eu.Location, eu.TypeName, eu.Result));
                        break;
                }

                return update;
            })
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(StreamSupervision.LoggingDecider(log)))
            .To(Sink.Ignore<EgressEvent>())
            .Run(Mat);

        log.Debug("gRPC snapshot consumer materialized — capturing forecasts and enrichments");
    }

    protected override void OnDependencyLost()
    {
        _sourceRef = null;
        _forecastActor = null;
        _enrichmentActor = null;
    }
}
