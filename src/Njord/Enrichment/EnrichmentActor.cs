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

        var pipelineActor = Context.GetActor<PipelineActor>();
        Context.Watch(pipelineActor);
        pipelineActor.Tell(new RequestPipelineSource());

        var egressActor = Context.GetActor<EgressActor>();
        Context.Watch(egressActor);
        egressActor.Tell(new RequestEgressSink());
    }

    private void WaitingForRefs()
    {
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
        if (_sourceRef is null || _egressSinkRef is null) return;

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

        var pipelineActor = Context.GetActor<PipelineActor>();
        Context.Watch(pipelineActor);
        pipelineActor.Tell(new RequestPipelineSource());

        var egressActor = Context.GetActor<EgressActor>();
        Context.Watch(egressActor);
        egressActor.Tell(new RequestEgressSink());

        Become(WaitingForRefs);
    }

    private void MaterializeEnrichmentGraph()
    {
        var mat = _mat!;

        var (snapshotHubSource, snapshotHubSink) = BroadcastHub.Sink<ModelSnapshot>(bufferSize: 64)
            .PreMaterialize(mat);

        _sourceRef!.Source
            .Scan(ModelSnapshot.Empty, (snap, outcome) => outcome switch
            {
                FetchOutcome.Success s => snap.Update(s.Forecast),
                _ => snap,
            })
            .Where(snap => snap.HasChanged)
            .To(snapshotHubSink)
            .Run(mat);

        var (egressMergeHubSink, egressMergeHubSource) = MergeHub.Source<EgressEvent>(perProducerBufferSize: 16)
            .PreMaterialize(mat);

        egressMergeHubSource
            .RunWith(_egressSinkRef!.Sink, mat);

        var locations = _options.Locations.Select(l => l.Name).ToList();

        foreach (var feature in _features)
        {
            if (!feature.Enabled) continue;

            switch (feature)
            {
                case IActorEnrichment actorFeature:
                    actorFeature.Materialize(snapshotHubSource, egressMergeHubSink, mat, Context);
                    break;

                case IStatefulEnrichment<Domain.Analysis.TrendResult> stateful:
                    MaterializeStateful(stateful, snapshotHubSource, egressMergeHubSink, mat, locations);
                    break;

                default:
                    MaterializeStateless(feature, snapshotHubSource, egressMergeHubSink, mat, locations);
                    break;
            }
        }
    }

    private static void MaterializeStateless(
        IEnrichmentFeature feature,
        Source<ModelSnapshot, NotUsed> source,
        Sink<EgressEvent, NotUsed> sink,
        IMaterializer mat,
        IReadOnlyList<string> locations)
    {
        var stateless = (dynamic)feature;

        source
            .SelectMany(snapshot => (IEnumerable<EgressEvent>)stateless.Compute(snapshot, locations))
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(
                _ => Akka.Streams.Supervision.Directive.Resume))
            .RunWith(sink, mat);
    }

    private static void MaterializeStateful<TResult>(
        IStatefulEnrichment<TResult> feature,
        Source<ModelSnapshot, NotUsed> source,
        Sink<EgressEvent, NotUsed> sink,
        IMaterializer mat,
        IReadOnlyList<string> locations)
    {
        ModelSnapshot? previousSnapshot = null;

        source
            .SelectMany(snapshot =>
            {
                var prev = previousSnapshot;
                previousSnapshot = snapshot;
                return feature.Compute(snapshot, prev, locations);
            })
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(
                _ => Akka.Streams.Supervision.Directive.Resume))
            .RunWith(sink, mat);
    }
}
