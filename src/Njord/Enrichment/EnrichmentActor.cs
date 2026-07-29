using Akka;
using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Ingest;
using Njord.Pipeline;
using Servus.Akka;

namespace Njord.Enrichment;

public sealed class EnrichmentActor : ReceiveActor, IWithStash
{
    private readonly NjordOptions _options;
    private readonly IReadOnlyList<IEnrichmentFeature> _features;
    private readonly ILogger<EnrichmentActor> _logger;

    private ISourceRef<FetchOutcome>? _sourceRef;
    private ISinkRef<EgressEvent>? _egressSinkRef;
    private IMaterializer? _mat;

    public IStash Stash { get; set; } = null!;

    private sealed record PipelineResolved(IActorRef Ref);
    private sealed record EgressResolved(IActorRef Ref);

    public EnrichmentActor(
        IOptions<NjordOptions> options,
        IEnumerable<IEnrichmentFeature> features,
        ILogger<EnrichmentActor> logger)
    {
        _options = options.Value;
        _features = [.. features];
        _logger = logger;

        WaitingForRefs();
    }

    protected override void PreStart()
    {
        _mat = Context.Materializer();
        ResolveUpstream();
    }

    private void ResolveUpstream()
    {
        Context.GetActorAsync<PipelineActor>().PipeTo(Self, success: r => new PipelineResolved(r));
        Context.GetActorAsync<EgressActor>().PipeTo(Self, success: r => new EgressResolved(r));
    }

    private void WaitingForRefs()
    {
        Receive<PipelineResolved>(msg =>
        {
            Context.Watch(msg.Ref);
            msg.Ref.Tell(new RequestPipelineSource());
        });
        Receive<EgressResolved>(msg =>
        {
            Context.Watch(msg.Ref);
            msg.Ref.Tell(new RequestEgressSink());
        });
        Receive<PipelineSourceResponse>(response =>
        {
            _sourceRef = response.SourceRef;
            _logger.LogInformation("Pipeline SourceRef received");
            TryTransitionToReady();
        });
        Receive<EgressSinkResponse>(response =>
        {
            _egressSinkRef = response.SinkRef;
            _logger.LogInformation("Egress SinkRef received");
            TryTransitionToReady();
        });
        Receive<Terminated>(msg => HandleTerminated(msg));
        ReceiveAny(_ => Stash.Stash());
    }

    private void TryTransitionToReady()
    {
        if (_sourceRef is null || _egressSinkRef is null)
            return;

        MaterializeEnrichmentGraph();
        _logger.LogInformation("Enrichment pipeline materialized — ready");
        Become(Ready);
        Stash.UnstashAll();
    }

    private void Ready()
    {
        Receive<Terminated>(msg => HandleTerminated(msg));
    }

    private void HandleTerminated(Terminated msg)
    {
        _logger.LogWarning("Watched actor {Actor} terminated — re-requesting refs", msg.ActorRef.Path.Name);

        _sourceRef = null;
        _egressSinkRef = null;

        ResolveUpstream();
        Become(WaitingForRefs);
    }

    private void MaterializeEnrichmentGraph()
    {
        var mat = _mat!;
        var locations = _options.Locations.Select(l => l.Name).ToList();

        var statelessFeatures = _features.OfType<IStatelessEnrichment>().Where(f => f.Enabled).ToList();
        var statefulFeatures = _features.OfType<IStatefulEnrichment>().Where(f => f.Enabled).ToList();
        var actorFeatures = _features.OfType<IActorEnrichment>().Where(f => f.Enabled).ToList();

        var flows = new List<Flow<ModelSnapshot, EgressEvent, NotUsed>>();

        if (statelessFeatures.Count > 0 || statefulFeatures.Count > 0)
            flows.Add(BuildInlineFlow(locations, statelessFeatures, statefulFeatures, _logger));

        foreach (var feature in actorFeatures)
            flows.Add(feature.CreateFlow(Context));

        if (flows.Count == 0)
            return;

        if (flows.Count == 1)
        {
            BuildScanSource(_sourceRef!.Source)
                .Via(flows[0])
                .RunWith(_egressSinkRef!.Sink, mat);
            return;
        }

        var graph = GraphDsl.Create(_egressSinkRef!.Sink, (builder, sink) =>
        {
            var source = builder.Add(BuildScanSource(_sourceRef!.Source));
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

        RunnableGraph.FromGraph(graph).Run(mat);
    }

    private static Flow<ModelSnapshot, EgressEvent, NotUsed> BuildInlineFlow(
        IReadOnlyList<string> locations,
        IReadOnlyList<IStatelessEnrichment> stateless,
        IReadOnlyList<IStatefulEnrichment> stateful,
        ILogger logger)
    {
        ModelSnapshot? previous = null;

        return Flow.Create<ModelSnapshot>()
            .SelectMany(snapshot =>
            {
                var prev = previous;
                previous = snapshot;
                return ComputeAll(snapshot, prev, locations, stateless, stateful);
            })
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(StreamSupervision.LoggingDecider(logger)));
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
        ModelSnapshot snapshot,
        ModelSnapshot? previous,
        IReadOnlyList<string> locations,
        IReadOnlyList<IStatelessEnrichment> stateless,
        IReadOnlyList<IStatefulEnrichment> stateful)
    {
        foreach (var feature in stateless)
            foreach (var evt in feature.Compute(snapshot, locations))
                yield return evt;

        foreach (var feature in stateful)
            foreach (var evt in feature.Compute(snapshot, previous, locations))
                yield return evt;
    }
}
