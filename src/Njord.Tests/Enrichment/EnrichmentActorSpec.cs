using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Logging.Abstractions;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Enrichment;
using Njord.Ingest;
using Njord.Pipeline;

namespace Njord.Tests.Enrichment;

[Collection("EnrichmentActor")]
public sealed class EnrichmentActorSpec : IDisposable
{
    private readonly ActorSystem _system = ActorSystem.Create("enrichment-spec");

    public void Dispose() => _system.Dispose();

    private static NjordOptions DefaultOptions() => new()
    {
        Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
        Models = ["icon_d2"],
    };

    private IActorRef CreateEnrichmentActor(
        EnrichmentOptions? enrichment = null)
    {
        var options = DefaultOptions();
        enrichment ??= new EnrichmentOptions();
        var optionsWrapped = Microsoft.Extensions.Options.Options.Create(options);
        var enrichmentWrapped = Microsoft.Extensions.Options.Options.Create(enrichment);
        var parameters = ParameterRegistry.Resolve(["Weather"], [], []);

        IEnumerable<IEnrichmentFeature> features = [];

        return _system.ActorOf(Props.Create(() => new EnrichmentActor(
            optionsWrapped,
            features,
            NullLogger<EnrichmentActor>.Instance)));
    }

    private async Task AssertActorAlive(IActorRef actor)
    {
        var identity = await actor.Ask<ActorIdentity>(new Identify(42), TimeSpan.FromSeconds(3));
        Assert.Equal(42, identity.MessageId);
    }

    [Fact(Timeout = 5000)]
    public async Task Requests_source_ref_from_pipeline_actor_on_startup()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();

        var fakePipeline = _system.ActorOf(Props.Create(() => new FakePipelineSource(mat)));
        var fakeEgress = _system.ActorOf(Props.Create(() => new FakeEgressSinkProvider(mat)));
        registry.Register<PipelineActor>(fakePipeline, overwrite: true);
        registry.Register<EgressActor>(fakeEgress, overwrite: true);

        var actor = CreateEnrichmentActor();

        await AssertActorAlive(actor);
    }

    [Fact(Timeout = 5000)]
    public async Task Disabled_consensus_does_not_crash()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();

        var fakePipeline = _system.ActorOf(Props.Create(() => new FakePipelineSource(mat)));
        var fakeEgress = _system.ActorOf(Props.Create(() => new FakeEgressSinkProvider(mat)));
        registry.Register<PipelineActor>(fakePipeline, overwrite: true);
        registry.Register<EgressActor>(fakeEgress, overwrite: true);

        var enrichment = new EnrichmentOptions { Consensus = new ConsensusOptions { Enabled = false } };
        var actor = CreateEnrichmentActor(enrichment);

        await AssertActorAlive(actor);
    }

    [Fact(Timeout = 5000)]
    public async Task Disabled_derived_does_not_crash()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();

        var fakePipeline = _system.ActorOf(Props.Create(() => new FakePipelineSource(mat)));
        var fakeEgress = _system.ActorOf(Props.Create(() => new FakeEgressSinkProvider(mat)));
        registry.Register<PipelineActor>(fakePipeline, overwrite: true);
        registry.Register<EgressActor>(fakeEgress, overwrite: true);

        var enrichment = new EnrichmentOptions { Derived = new DerivedOptions { Enabled = false } };
        var actor = CreateEnrichmentActor(enrichment);

        await AssertActorAlive(actor);
    }

    [Fact(Timeout = 5000)]
    public async Task All_consumers_enabled_does_not_crash()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();

        var fakePipeline = _system.ActorOf(Props.Create(() => new FakePipelineSource(mat)));
        var fakeEgress = _system.ActorOf(Props.Create(() => new FakeEgressSinkProvider(mat)));
        registry.Register<PipelineActor>(fakePipeline, overwrite: true);
        registry.Register<EgressActor>(fakeEgress, overwrite: true);

        var enrichment = new EnrichmentOptions
        {
            Consensus = new ConsensusOptions { Enabled = true },
            Alerts = new AlertThresholdOptions { Enabled = true },
            Derived = new DerivedOptions { Enabled = true },
        };
        var actor = CreateEnrichmentActor(enrichment);

        await AssertActorAlive(actor);
    }

    [Fact(Timeout = 5000)]
    public async Task Disabled_trends_does_not_crash()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();

        var fakePipeline = _system.ActorOf(Props.Create(() => new FakePipelineSource(mat)));
        var fakeEgress = _system.ActorOf(Props.Create(() => new FakeEgressSinkProvider(mat)));
        registry.Register<PipelineActor>(fakePipeline, overwrite: true);
        registry.Register<EgressActor>(fakeEgress, overwrite: true);

        var enrichment = new EnrichmentOptions { Trends = new TrendOptions { Enabled = false } };
        var actor = CreateEnrichmentActor(enrichment);

        await AssertActorAlive(actor);
    }

    [Fact(Timeout = 5000)]
    public async Task Disabled_history_does_not_crash()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();

        var fakePipeline = _system.ActorOf(Props.Create(() => new FakePipelineSource(mat)));
        var fakeEgress = _system.ActorOf(Props.Create(() => new FakeEgressSinkProvider(mat)));
        registry.Register<PipelineActor>(fakePipeline, overwrite: true);
        registry.Register<EgressActor>(fakeEgress, overwrite: true);

        var enrichment = new EnrichmentOptions { History = new HistoryOptions { Enabled = false } };
        var actor = CreateEnrichmentActor(enrichment);

        await AssertActorAlive(actor);
    }

    [Fact(Timeout = 5000)]
    public async Task Disabled_energy_does_not_crash()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();

        var fakePipeline = _system.ActorOf(Props.Create(() => new FakePipelineSource(mat)));
        var fakeEgress = _system.ActorOf(Props.Create(() => new FakeEgressSinkProvider(mat)));
        registry.Register<PipelineActor>(fakePipeline, overwrite: true);
        registry.Register<EgressActor>(fakeEgress, overwrite: true);

        var enrichment = new EnrichmentOptions { Energy = new EnergyOptions { Enabled = false } };
        var actor = CreateEnrichmentActor(enrichment);

        await AssertActorAlive(actor);
    }

    [Fact(Timeout = 5000)]
    public async Task Disabled_indices_does_not_crash()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();

        var fakePipeline = _system.ActorOf(Props.Create(() => new FakePipelineSource(mat)));
        var fakeEgress = _system.ActorOf(Props.Create(() => new FakeEgressSinkProvider(mat)));
        registry.Register<PipelineActor>(fakePipeline, overwrite: true);
        registry.Register<EgressActor>(fakeEgress, overwrite: true);

        var enrichment = new EnrichmentOptions { Indices = new IndexOptions { Enabled = false } };
        var actor = CreateEnrichmentActor(enrichment);

        await AssertActorAlive(actor);
    }

    [Fact(Timeout = 5000)]
    public async Task Enabled_trends_does_not_crash()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();

        var fakePipeline = _system.ActorOf(Props.Create(() => new FakePipelineSource(mat)));
        var fakeEgress = _system.ActorOf(Props.Create(() => new FakeEgressSinkProvider(mat)));
        registry.Register<PipelineActor>(fakePipeline, overwrite: true);
        registry.Register<EgressActor>(fakeEgress, overwrite: true);

        var enrichment = new EnrichmentOptions { Trends = new TrendOptions { Enabled = true } };
        var actor = CreateEnrichmentActor(enrichment);

        await AssertActorAlive(actor);
    }

    private sealed class FakePipelineSource : ReceiveActor
    {
        public FakePipelineSource(IMaterializer mat)
        {
            Receive<RequestPipelineSource>(_ =>
            {
                var sourceRef = Source.Empty<FetchOutcome>()
                    .RunWith(StreamRefs.SourceRef<FetchOutcome>(), mat)
                    .Result;
                Sender.Tell(new PipelineSourceResponse(sourceRef));
            });
        }
    }

    private sealed class FakeEgressSinkProvider : ReceiveActor
    {
        public FakeEgressSinkProvider(IMaterializer mat)
        {
            Receive<RequestEgressSink>(_ =>
            {
                var sinkRef = StreamRefs.SinkRef<EgressEvent>()
                    .To(Sink.Ignore<EgressEvent>().MapMaterializedValue(_ => Akka.NotUsed.Instance))
                    .Run(mat);
                sinkRef.PipeTo(Sender, Self,
                    sr => new EgressSinkResponse(sr),
                    _ => null!);
            });
        }
    }
}
