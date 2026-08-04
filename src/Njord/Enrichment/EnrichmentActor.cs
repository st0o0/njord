using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Actors;
using Njord.Configuration;
using Njord.Domain.Analysis;
using Njord.Domain.Weather;
using Njord.Domain.Sensors;
using Njord.Egress;
using Njord.Pipeline;
using Njord.Sensors;
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
    private ISinkRef<EgressEvent>? _egressSinkRef;
    private IActorRef? _sensorHub;

    private sealed record PipelineResolved(IActorRef Ref);
    private sealed record EgressResolved(IActorRef Ref);
    private sealed record SensorHubResolved(IActorRef Ref);

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

    protected override void PreStart()
    {
        _log = Context.GetLogger();
        base.PreStart();
    }

    protected override void ResolveDependencies()
    {
        Context.GetActorAsync<PipelineActor>().PipeTo(Self, success: r => new PipelineResolved(r));
        Context.GetActorAsync<EgressActor>().PipeTo(Self, success: r => new EgressResolved(r));
        Context.GetActorAsync<SensorHubActor>().PipeTo(Self, success: r => new SensorHubResolved(r));
    }

    protected override void ConfigureWaitingForRefs()
    {
        Receive<PipelineResolved>(msg =>
        {
            if (IsDeadRef(msg.Ref)) { ScheduleRetryResolve(); return; }
            TrackDependency(msg.Ref);
            msg.Ref.Tell(new RequestPipelineSource());
        });
        Receive<EgressResolved>(msg =>
        {
            if (IsDeadRef(msg.Ref)) { ScheduleRetryResolve(); return; }
            TrackDependency(msg.Ref);
            msg.Ref.Tell(new RequestEgressSink());
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
            _sourceRef = response.SourceRef;
            _log.Debug("SourceRef received from {Source}", Sender.Path);
            TryTransition();
        });
        Receive<EgressSinkResponse>(response =>
        {
            _egressSinkRef = response.SinkRef;
            _log.Debug("SinkRef received from {Source}", Sender.Path);
            TryTransition();
        });
    }

    protected override bool AllRefsReady() => _sourceRef is not null && _egressSinkRef is not null && _sensorHub is not null;

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

        var consensusInlineFlow = BuildConsensusInlineFlow(
            consensusEgressEnabled, locations, statelessFeatures, statefulFeatures, _sensorHub!, _log);

        var flows = new List<Flow<ModelSnapshot, EgressEvent, NotUsed>>();

        if (hasInlineEnrichments || consensusEgressEnabled)
        {
            flows.Add(consensusFlow.Via(consensusInlineFlow));
        }

        foreach (var feature in actorFeatures)
            flows.Add(feature.CreateFlow(Context));

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

        if (flows.Count == 1)
        {
            snapshotSource
                .Via(flows[0])
                .Via(logOut)
                .RunWith(_egressSinkRef!.Sink, Mat);
            return;
        }

        var graph = GraphDsl.Create(_egressSinkRef!.Sink, (builder, sink) =>
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

    protected override void OnDependencyLost()
    {
        _sourceRef = null;
        _egressSinkRef = null;
        _sensorHub = null;
    }

    private static Flow<ConsensusSnapshot, EgressEvent, NotUsed> BuildConsensusInlineFlow(
        bool consensusEgressEnabled,
        IReadOnlyList<string> locations,
        IReadOnlyList<IStatelessEnrichment> stateless,
        IReadOnlyList<IStatefulEnrichment> stateful,
        IActorRef sensorHub,
        ILoggingAdapter log)
    {
        ConsensusSnapshot? previous = null;

        return Flow.Create<ConsensusSnapshot>()
            .SelectAsync(1, async consensus =>
            {
                SensorSnapshot? sensors = null;
                try
                {
                    var response = await sensorHub.Ask<SensorSnapshotResponse>(
                        new GetSnapshot(consensus.Location), TimeSpan.FromSeconds(1));
                    sensors = response.Snapshot;
                }
                catch
                {
                    log.Warning("SensorHub Ask timed out for {Location}, proceeding without sensor data", consensus.Location);
                }

                var prev = previous;
                previous = consensus;
                var events = ComputeAll(consensus, prev, sensors, consensusEgressEnabled, stateless, stateful).ToList();
                if (events.Count > 0)
                {
                    var features = string.Join(
                        ", ",
                        events
                            .Select(e => e is EgressEvent.EnrichmentUpdate eu ? eu.TypeName : null)
                            .Where(t => t is not null)
                            .Distinct());
                    log.Info("Enrichment computed for {Location}: {Features}", consensus.Location, features);
                }

                return events;
            })
            .SelectMany(events => events)
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(StreamSupervision.LoggingDecider(log)));
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

    private static IEnumerable<EgressEvent> ComputeAll(
        ConsensusSnapshot consensus,
        ConsensusSnapshot? previous,
        SensorSnapshot? sensors,
        bool consensusEgressEnabled,
        IReadOnlyList<IStatelessEnrichment> stateless,
        IReadOnlyList<IStatefulEnrichment> stateful)
    {
        if (consensusEgressEnabled)
        {
            var result = new ConsensusResult(consensus.Hourly.Parameters, consensus.Daily.Parameters, consensus.ComputedAt);
            yield return new EgressEvent.EnrichmentUpdate(consensus.Location, "consensus", result, consensus.ComputedAt);
        }

        foreach (var feature in stateless)
            foreach (var evt in feature.Compute(consensus, sensors))
                yield return evt;

        foreach (var feature in stateful)
            foreach (var evt in feature.Compute(consensus, previous, sensors))
                yield return evt;
    }
}
