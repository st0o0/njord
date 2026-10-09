using System.Diagnostics;
using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Compute.Analysis;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Messages.Egress;
using Njord.Messages.Sensors;
using Njord.Sensors;
using Njord.Sensors;
using Njord.Weather;
using Njord.Weather;
using Servus.Akka;

namespace Njord.Enrichment;

public sealed class EnrichmentActor : StreamConsumerActor
{

    private readonly NjordOptions _options;
    private readonly EnrichmentOptions _enrichmentOptions;
    private readonly ConsensusSnapshotFactory _consensusFactory;
    private readonly IReadOnlyList<IEnrichmentFeature> _features;
    private ILoggingAdapter _log = null!;

    private ISourceRef<FetchOutcome>? _sourceRef;
    private IActorRef? _sensorHub;
    private long _pipelineSourceRequestId;
    private Source<EgressEvent, NotUsed>? _broadcastHubSource;

    private sealed record PipelineResolved(IActorRef Ref);
    private sealed record SensorHubResolved(IActorRef Ref);
    private sealed record PipelineResolveFailed(Exception Cause);
    private sealed record SensorHubResolveFailed(Exception Cause);

    public EnrichmentActor(
        IOptions<NjordOptions> options,
        IOptions<EnrichmentOptions> enrichmentOptions,
        ConsensusSnapshotFactory consensusFactory,
        IEnumerable<IEnrichmentFeature> features)
    {
        _options = options.Value;
        _enrichmentOptions = enrichmentOptions.Value;
        _consensusFactory = consensusFactory;
        _features = [.. features];
    }

    private IActorRef? _pipelineRef;

    protected override void PreStart()
    {
        _log = Context.GetLogger();
        base.PreStart();
    }

    protected override void ResolveInitialDependencies()
    {
        _pipelineRef = Context.GetActor<IPipelineActor>();
        TrackDependency(_pipelineRef);

        _sensorHub = Context.GetActor<ISensorHubActor>();
    }

    protected override void RequestSourceRefs()
    {
        var id = NextRequestId();
        _pipelineSourceRequestId = id;
        _pipelineRef!.Tell(new RequestPipelineSource(id));
    }

    protected override void ResolveDependencies()
    {
        Context.GetActorAsync<IPipelineActor>().PipeTo(Self, success: r => new PipelineResolved(r), failure: ex => new PipelineResolveFailed(ex));
        Context.GetActorAsync<ISensorHubActor>().PipeTo(Self, success: r => new SensorHubResolved(r), failure: ex => new SensorHubResolveFailed(ex));
    }

    protected override void ConfigureWaitingForRefs()
    {
        Receive<PipelineResolved>(msg =>
        {
            if (IsDeadRef(msg.Ref)) { ScheduleRetryResolve(); return; }
            TrackDependency(msg.Ref);
            var id = NextRequestId();
            _pipelineSourceRequestId = id;
            msg.Ref.Tell(new RequestPipelineSource(id));
        });
        Receive<SensorHubResolved>(msg =>
        {
            if (IsDeadRef(msg.Ref)) { ScheduleRetryResolve(); return; }
            _sensorHub = msg.Ref;
            _log.Debug("SensorHub resolved at {Path}", msg.Ref.Path);
            TryTransition();
        });
        Receive<PipelineSourceResponse>(response =>
        {
            if (response.RequestId != _pipelineSourceRequestId)
            {
                return;
            }

            _sourceRef = response.SourceRef;
            _log.Debug("SourceRef received from {Source}", Sender.Path);
            TryTransition();
        });
        Receive<PipelineSourceFailed>(msg =>
        {
            if (msg.RequestId != _pipelineSourceRequestId)
            {
                return;
            }

            _log.Warning(msg.Cause, "Pipeline source request failed - retrying");
            ScheduleRetryResolve();
        });
        Receive<PipelineResolveFailed>(msg =>
        {
            _log.Warning(msg.Cause, "Failed to resolve PipelineActor - retrying");
            ScheduleRetryResolve();
        });
        Receive<SensorHubResolveFailed>(msg =>
        {
            _log.Warning(msg.Cause, "Failed to resolve SensorHubActor - retrying");
            ScheduleRetryResolve();
        });
    }

    protected override bool AllRefsReady() => _sourceRef is not null && _sensorHub is not null;

    protected override void MaterializeGraph(SharedKillSwitch killSwitch)
    {
        var locations = _options.Locations.Select(l => l.Name).ToList();
        var trimPercent = _enrichmentOptions.Consensus.TrimPercent;
        var consensusEgressEnabled = _enrichmentOptions.Consensus.Enabled;

        var statelessFeatures = _features.OfType<IStatelessEnrichment>().Where(f => f.Enabled).ToList();
        var statefulFeatures = _features.OfType<IStatefulEnrichment>().Where(f => f.Enabled).ToList();
        var actorFeatures = _features.OfType<IActorEnrichment>().Where(f => f.Enabled).ToList();

        var hasInlineEnrichments = statelessFeatures.Count > 0 || statefulFeatures.Count > 0;
        var hasActorEnrichments = actorFeatures.Count > 0;

        if (!hasInlineEnrichments && !hasActorEnrichments && !consensusEgressEnabled)
        {
            return;
        }

        var snapshotSource = BuildScanSource(_sourceRef!.Source)
            .Log("enrichment-snapshot", s => $"{s.Entries.Count} models changed={s.HasChanged}", _log)
            .Via(killSwitch.Flow<ModelSnapshot>());

        var consensusFlow = Flow.Create<ModelSnapshot>()
            .SelectMany(snapshot => ComputeConsensus(snapshot, locations, trimPercent));

        var consensusInlineFlow = ConsensusFlowFactory.Create(
            _sensorHub!, TimeSpan.FromSeconds(1), consensusEgressEnabled, statelessFeatures, statefulFeatures, _log);

        var flows = new List<Flow<ModelSnapshot, EgressEvent, NotUsed>>();

        if (hasInlineEnrichments || consensusEgressEnabled)
        {
            flows.Add(consensusFlow.Via(consensusInlineFlow));
        }

        foreach (var feature in actorFeatures)
        {
            flows.Add(feature.CreateFlow(Context));
        }

        if (flows.Count == 0)
        {
            return;
        }

        var logOut = Flow.Create<EgressEvent>()
            .Log("enrichment-out", e => e switch
            {
                EgressEvent.EnrichmentUpdate u => $"{u.Location}/{u.TypeName}",
                _ => "?",
            }, _log);

        var (broadcastHubSource, broadcastHubSink) = BroadcastHub.Sink<EgressEvent>(bufferSize: 4)
            .PreMaterialize(Mat);

        _broadcastHubSource = broadcastHubSource;

        if (flows.Count == 1)
        {
            snapshotSource
                .Via(flows[0])
                .Via(logOut)
                .To(broadcastHubSink)
                .Run(Mat);
            return;
        }

        var graph = GraphDsl.Create(broadcastHubSink, (builder, sink) =>
        {
            var source = builder.Add(snapshotSource);
            var broadcast = builder.Add(new Broadcast<ModelSnapshot>(flows.Count));
            var merge = builder.Add(new Merge<EgressEvent>(flows.Count));
            var logStage = builder.Add(logOut);

            builder.From(source).To(broadcast);

            for (var i = 0; i < flows.Count; i++)
            {
                builder.From(broadcast.Out(i))
                    .Via(builder.Add(flows[i]))
                    .To(merge.In(i));
            }

            builder.From(merge).Via(logStage).To(sink);
            return ClosedShape.Instance;
        });

        RunnableGraph.FromGraph(graph).Run(Mat);
    }

    protected override void ConfigureReady()
    {
        Receive<RequestEnrichmentSource>(msg =>
        {
            _broadcastHubSource!
                .RunWith(StreamRefs.SourceRef<EgressEvent>(), Mat)
                .PipeTo(Sender, Self,
                    sr => new EnrichmentSourceResponse(msg.RequestId, sr),
                    ex =>
                    {
                        _log.Error(ex, "Failed to create Enrichment SourceRef");
                        return new EnrichmentSourceFailed(msg.RequestId, ex);
                    });
        });
    }

    protected override void OnDependencyLost()
    {
        _pipelineRef = null;
        _sourceRef = null;
        _sensorHub = null;
        _pipelineSourceRequestId = 0;
        _broadcastHubSource = null;
    }

    private IEnumerable<ConsensusSnapshot> ComputeConsensus(
        ModelSnapshot snapshot,
        IReadOnlyList<string> locations,
        double trimPercent)
    {
        foreach (var location in locations)
        {
            yield return _consensusFactory.Create(snapshot, location, trimPercent);
        }
    }

    private Source<ModelSnapshot, NotUsed> BuildScanSource(Source<FetchOutcome, NotUsed> source)
    {
        return source
            .Scan(ModelSnapshot.Empty, (snap, outcome) => outcome switch
            {
                FetchOutcome.Success s => snap.Update(s.Forecast),
                _ => snap,
            })
            .Where(snap => snap.HasChanged);
    }
}
