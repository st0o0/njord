using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Actors;
using Njord.Messages.Common;
using Njord.Messages.Egress;
using Njord.Messages.Snapshots;
using Servus.Akka;

namespace Njord.Grpc;

public sealed class GrpcSnapshotConsumerActor : StreamConsumerActor
{
    private IActorRef? _modelStateRef;
    private IActorRef? _enrichmentRef;
    private ISourceRef<EgressEvent>? _modelStateSourceRef;
    private ISourceRef<EgressEvent>? _enrichmentSourceRef;
    private IActorRef? _forecastActor;
    private IActorRef? _enrichmentSnapshotActor;
    private long _modelStateSourceRequestId;
    private long _enrichmentSourceRequestId;

    private sealed record ModelStateResolved(IActorRef Ref);
    private sealed record ModelStateResolveFailed(Exception Cause);
    private sealed record EnrichmentResolved(IActorRef Ref);
    private sealed record EnrichmentResolveFailed(Exception Cause);
    private sealed record SnapshotActorsResolved(IActorRef Forecast, IActorRef Enrichment);
    private sealed record SnapshotResolveFailed(Exception Cause);

    protected override void ResolveInitialDependencies()
    {
        _modelStateRef = Context.GetActor<IModelStateActor>();
        TrackDependency(_modelStateRef);

        _enrichmentRef = Context.GetActor<IEnrichmentActor>();
        TrackDependency(_enrichmentRef);

        _forecastActor = Context.GetActor<IForecastSnapshotActor>();
        _enrichmentSnapshotActor = Context.GetActor<IEnrichmentSnapshotActor>();
    }

    protected override void RequestSourceRefs()
    {
        var modelId = NextRequestId();
        _modelStateSourceRequestId = modelId;
        _modelStateRef!.Tell(new RequestModelStateSource(modelId));

        var enrichId = NextRequestId();
        _enrichmentSourceRequestId = enrichId;
        _enrichmentRef!.Tell(new RequestEnrichmentSource(enrichId));
    }

    protected override void ResolveDependencies()
    {
        Context.GetActorAsync<IModelStateActor>().PipeTo(Self, success: r => new ModelStateResolved(r), failure: ex => new ModelStateResolveFailed(ex));
        Context.GetActorAsync<IEnrichmentActor>().PipeTo(Self, success: r => new EnrichmentResolved(r), failure: ex => new EnrichmentResolveFailed(ex));
    }

    protected override void ConfigureWaitingForRefs()
    {
        Receive<ModelStateResolved>(msg =>
        {
            if (IsDeadRef(msg.Ref))
            {
                ScheduleRetryResolve();
                return;
            }

            TrackDependency(msg.Ref);
            var id = NextRequestId();
            _modelStateSourceRequestId = id;
            msg.Ref.Tell(new RequestModelStateSource(id));
        });
        Receive<ModelStateSourceResponse>(response =>
        {
            if (response.RequestId != _modelStateSourceRequestId)
            {
                return;
            }

            _modelStateSourceRef = response.SourceRef;
            TryResolveSnapshotActors();
        });
        Receive<ModelStateSourceFailed>(msg =>
        {
            if (msg.RequestId != _modelStateSourceRequestId)
            {
                return;
            }

            Context.GetLogger().Warning(msg.Cause, "ModelState source request failed - retrying");
            ScheduleRetryResolve();
        });
        Receive<ModelStateResolveFailed>(msg =>
        {
            Context.GetLogger().Warning(msg.Cause, "Failed to resolve ModelStateActor - retrying");
            ScheduleRetryResolve();
        });

        Receive<EnrichmentResolved>(msg =>
        {
            if (IsDeadRef(msg.Ref))
            {
                ScheduleRetryResolve();
                return;
            }

            TrackDependency(msg.Ref);
            var id = NextRequestId();
            _enrichmentSourceRequestId = id;
            msg.Ref.Tell(new RequestEnrichmentSource(id));
        });
        Receive<EnrichmentSourceResponse>(response =>
        {
            if (response.RequestId != _enrichmentSourceRequestId)
            {
                return;
            }

            _enrichmentSourceRef = response.SourceRef;
            TryResolveSnapshotActors();
        });
        Receive<EnrichmentSourceFailed>(msg =>
        {
            if (msg.RequestId != _enrichmentSourceRequestId)
            {
                return;
            }

            Context.GetLogger().Warning(msg.Cause, "Enrichment source request failed - retrying");
            ScheduleRetryResolve();
        });
        Receive<EnrichmentResolveFailed>(msg =>
        {
            Context.GetLogger().Warning(msg.Cause, "Failed to resolve EnrichmentActor - retrying");
            ScheduleRetryResolve();
        });

        Receive<SnapshotActorsResolved>(msg =>
        {
            _forecastActor = msg.Forecast;
            _enrichmentSnapshotActor = msg.Enrichment;
            TryTransition();
        });
        Receive<SnapshotResolveFailed>(msg =>
        {
            Context.GetLogger().Warning(msg.Cause, "Failed to resolve snapshot actors - retrying");
            ScheduleRetryResolve();
        });
    }

    private void TryResolveSnapshotActors()
    {
        if (_modelStateSourceRef is null || _enrichmentSourceRef is null)
        {
            return;
        }

        var forecastTask = Context.GetActorAsync<IForecastSnapshotActor>();
        var enrichmentTask = Context.GetActorAsync<IEnrichmentSnapshotActor>();
        Task.WhenAll(forecastTask, enrichmentTask)
            .PipeTo(Self, success: _ => new SnapshotActorsResolved(forecastTask.Result, enrichmentTask.Result), failure: ex => new SnapshotResolveFailed(ex));
    }

    protected override bool AllRefsReady() =>
        _modelStateSourceRef is not null && _enrichmentSourceRef is not null
        && _forecastActor is not null && _enrichmentSnapshotActor is not null;

    protected override void MaterializeGraph(SharedKillSwitch killSwitch)
    {
        var log = Context.GetLogger();
        var forecastActor = _forecastActor!;
        var enrichmentSnapshotActor = _enrichmentSnapshotActor!;

        var modelStateSource = _modelStateSourceRef!.Source;
        var enrichmentSource = _enrichmentSourceRef!.Source;

        modelStateSource.Merge(enrichmentSource)
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
                        await enrichmentSnapshotActor.Ask<Ack>(
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
        _modelStateRef = null;
        _enrichmentRef = null;
        _modelStateSourceRef = null;
        _enrichmentSourceRef = null;
        _forecastActor = null;
        _enrichmentSnapshotActor = null;
        _modelStateSourceRequestId = 0;
        _enrichmentSourceRequestId = 0;
    }
}
