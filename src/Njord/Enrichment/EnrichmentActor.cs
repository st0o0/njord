using Akka;
using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Actors;
using Njord.Configuration;
using Njord.Domain.Analysis;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Pipeline;
using Servus.Akka;

namespace Njord.Enrichment;

public sealed class EnrichmentActor : StreamConsumerActor
{
    private readonly NjordOptions _options;
    private readonly EnrichmentOptions _enrichmentOptions;
    private readonly ResolvedParameterSet _parameters;
    private readonly TimeProvider _timeProvider;
    private readonly IReadOnlyList<IEnrichmentFeature> _features;
    private readonly ILogger<EnrichmentActor> _logger;

    private ISourceRef<FetchOutcome>? _sourceRef;
    private ISinkRef<EgressEvent>? _egressSinkRef;

    private sealed record PipelineResolved(IActorRef Ref);
    private sealed record EgressResolved(IActorRef Ref);

    public EnrichmentActor(
        IOptions<NjordOptions> options,
        IOptions<EnrichmentOptions> enrichmentOptions,
        ResolvedParameterSet parameters,
        TimeProvider timeProvider,
        IEnumerable<IEnrichmentFeature> features,
        ILogger<EnrichmentActor> logger)
    {
        _options = options.Value;
        _enrichmentOptions = enrichmentOptions.Value;
        _parameters = parameters;
        _timeProvider = timeProvider;
        _features = [.. features];
        _logger = logger;
    }

    protected override void ResolveDependencies()
    {
        Context.GetActorAsync<PipelineActor>().PipeTo(Self, success: r => new PipelineResolved(r));
        Context.GetActorAsync<EgressActor>().PipeTo(Self, success: r => new EgressResolved(r));
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
        Receive<PipelineSourceResponse>(response =>
        {
            _sourceRef = response.SourceRef;
            _logger.LogInformation("Pipeline SourceRef received");
            TryTransition();
        });
        Receive<EgressSinkResponse>(response =>
        {
            _egressSinkRef = response.SinkRef;
            _logger.LogInformation("Egress SinkRef received");
            TryTransition();
        });
    }

    protected override bool AllRefsReady() => _sourceRef is not null && _egressSinkRef is not null;

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
            .Via(killSwitch.Flow<ModelSnapshot>());

        var consensusFlow = Flow.Create<ModelSnapshot>()
            .SelectMany(snapshot => ComputeConsensus(snapshot, locations, trimPercent));

        var consensusInlineFlow = BuildConsensusInlineFlow(
            consensusEgressEnabled, locations, statelessFeatures, statefulFeatures, _logger);

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

        if (flows.Count == 1)
        {
            snapshotSource
                .Via(flows[0])
                .RunWith(_egressSinkRef!.Sink, Mat);
            return;
        }

        var graph = GraphDsl.Create(_egressSinkRef!.Sink, (builder, sink) =>
        {
            var source = builder.Add(snapshotSource);
            var broadcast = builder.Add(new Broadcast<ModelSnapshot>(flows.Count));
            var merge = builder.Add(new Merge<EgressEvent>(flows.Count));

            builder.From(source).To(broadcast);

            for (var i = 0; i < flows.Count; i++)
            {
                builder.From(broadcast.Out(i))
                    .Via(builder.Add(flows[i]))
                    .To(merge.In(i));
            }

            builder.From(merge).To(sink);
            return ClosedShape.Instance;
        });

        RunnableGraph.FromGraph(graph).Run(Mat);
    }

    protected override void OnDependencyLost()
    {
        _sourceRef = null;
        _egressSinkRef = null;
    }

    private static Flow<ConsensusSnapshot, EgressEvent, NotUsed> BuildConsensusInlineFlow(
        bool consensusEgressEnabled,
        IReadOnlyList<string> locations,
        IReadOnlyList<IStatelessEnrichment> stateless,
        IReadOnlyList<IStatefulEnrichment> stateful,
        ILogger logger)
    {
        ConsensusSnapshot? previous = null;

        return Flow.Create<ConsensusSnapshot>()
            .SelectMany(consensus =>
            {
                var prev = previous;
                previous = consensus;
                return ComputeAll(consensus, prev, consensusEgressEnabled, stateless, stateful);
            })
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(StreamSupervision.LoggingDecider(logger)));
    }

    private IEnumerable<ConsensusSnapshot> ComputeConsensus(
        ModelSnapshot snapshot,
        IReadOnlyList<string> locations,
        double trimPercent)
    {
        foreach (var location in locations)
        {
            yield return ConsensusSnapshot.Compute(
                snapshot, _parameters, location, _timeProvider, trimPercent);
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
        bool consensusEgressEnabled,
        IReadOnlyList<IStatelessEnrichment> stateless,
        IReadOnlyList<IStatefulEnrichment> stateful)
    {
        if (consensusEgressEnabled)
        {
            var result = new ConsensusResult(consensus.Hourly.Parameters, consensus.Daily.Parameters);
            yield return new EgressEvent.EnrichmentUpdate(consensus.Location, "consensus", result);
        }

        foreach (var feature in stateless)
            foreach (var evt in feature.Compute(consensus))
                yield return evt;

        foreach (var feature in stateful)
            foreach (var evt in feature.Compute(consensus, previous))
                yield return evt;
    }
}
