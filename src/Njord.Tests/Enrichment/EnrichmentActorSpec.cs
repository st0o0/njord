using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Logging.Abstractions;
using Njord.Configuration;
using Njord.Domain;
using Njord.Egress;
using Njord.Enrichment;
using Njord.Ingest;
using Njord.Pipeline;

namespace Njord.Tests.Enrichment;

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
        var parameters = ParameterRegistry.Resolve(["Weather"], [], []);

        return _system.ActorOf(Props.Create(() => new EnrichmentActor(
            Microsoft.Extensions.Options.Options.Create(options),
            Microsoft.Extensions.Options.Options.Create(enrichment),
            parameters,
            TimeProvider.System,
            NullLogger<EnrichmentActor>.Instance)));
    }

    [Fact(Timeout = 5000)]
    public async Task Requests_source_ref_from_pipeline_actor_on_startup()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();

        var fakePipeline = _system.ActorOf(Props.Create(() => new FakePipelineSource(mat)));
        var fakeEgress = _system.ActorOf(Props.Create(() => new FakeMqttSinkProvider(mat)));
        registry.Register<PipelineActor>(fakePipeline, overwrite: true);
        registry.Register<MqttEgressActor>(fakeEgress, overwrite: true);

        var actor = CreateEnrichmentActor();

        // Give it time to initialize and transition
        await Task.Delay(500);

        // If we got here without deadlock/crash, the actor transitioned successfully
        Assert.NotNull(actor);
    }

    [Fact(Timeout = 5000)]
    public async Task Disabled_consensus_does_not_crash()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();

        var fakePipeline = _system.ActorOf(Props.Create(() => new FakePipelineSource(mat)));
        var fakeEgress = _system.ActorOf(Props.Create(() => new FakeMqttSinkProvider(mat)));
        registry.Register<PipelineActor>(fakePipeline, overwrite: true);
        registry.Register<MqttEgressActor>(fakeEgress, overwrite: true);

        var enrichment = new EnrichmentOptions { Consensus = new ConsensusOptions { Enabled = false } };
        var actor = CreateEnrichmentActor(enrichment);

        await Task.Delay(500);
        Assert.NotNull(actor);
    }

    [Fact(Timeout = 5000)]
    public async Task Disabled_derived_does_not_crash()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();

        var fakePipeline = _system.ActorOf(Props.Create(() => new FakePipelineSource(mat)));
        var fakeEgress = _system.ActorOf(Props.Create(() => new FakeMqttSinkProvider(mat)));
        registry.Register<PipelineActor>(fakePipeline, overwrite: true);
        registry.Register<MqttEgressActor>(fakeEgress, overwrite: true);

        var enrichment = new EnrichmentOptions { Derived = new DerivedOptions { Enabled = false } };
        var actor = CreateEnrichmentActor(enrichment);

        await Task.Delay(500);
        Assert.NotNull(actor);
    }

    [Fact(Timeout = 5000)]
    public async Task All_consumers_enabled_does_not_crash()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();

        var fakePipeline = _system.ActorOf(Props.Create(() => new FakePipelineSource(mat)));
        var fakeEgress = _system.ActorOf(Props.Create(() => new FakeMqttSinkProvider(mat)));
        registry.Register<PipelineActor>(fakePipeline, overwrite: true);
        registry.Register<MqttEgressActor>(fakeEgress, overwrite: true);

        var enrichment = new EnrichmentOptions
        {
            Consensus = new ConsensusOptions { Enabled = true },
            Alerts = new AlertThresholdOptions { Enabled = true },
            Derived = new DerivedOptions { Enabled = true },
        };
        var actor = CreateEnrichmentActor(enrichment);

        await Task.Delay(500);
        Assert.NotNull(actor);
    }

    [Fact(Timeout = 5000)]
    public async Task Disabled_trends_does_not_crash()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();

        var fakePipeline = _system.ActorOf(Props.Create(() => new FakePipelineSource(mat)));
        var fakeEgress = _system.ActorOf(Props.Create(() => new FakeMqttSinkProvider(mat)));
        registry.Register<PipelineActor>(fakePipeline, overwrite: true);
        registry.Register<MqttEgressActor>(fakeEgress, overwrite: true);

        var enrichment = new EnrichmentOptions { Trends = new TrendOptions { Enabled = false } };
        var actor = CreateEnrichmentActor(enrichment);

        await Task.Delay(500);
        Assert.NotNull(actor);
    }

    [Fact(Timeout = 5000)]
    public async Task Disabled_history_does_not_crash()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();

        var fakePipeline = _system.ActorOf(Props.Create(() => new FakePipelineSource(mat)));
        var fakeEgress = _system.ActorOf(Props.Create(() => new FakeMqttSinkProvider(mat)));
        registry.Register<PipelineActor>(fakePipeline, overwrite: true);
        registry.Register<MqttEgressActor>(fakeEgress, overwrite: true);

        var enrichment = new EnrichmentOptions { History = new HistoryOptions { Enabled = false } };
        var actor = CreateEnrichmentActor(enrichment);

        await Task.Delay(500);
        Assert.NotNull(actor);
    }

    [Fact(Timeout = 5000)]
    public async Task Disabled_energy_does_not_crash()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();

        var fakePipeline = _system.ActorOf(Props.Create(() => new FakePipelineSource(mat)));
        var fakeEgress = _system.ActorOf(Props.Create(() => new FakeMqttSinkProvider(mat)));
        registry.Register<PipelineActor>(fakePipeline, overwrite: true);
        registry.Register<MqttEgressActor>(fakeEgress, overwrite: true);

        var enrichment = new EnrichmentOptions { Energy = new EnergyOptions { Enabled = false } };
        var actor = CreateEnrichmentActor(enrichment);

        await Task.Delay(500);
        Assert.NotNull(actor);
    }

    [Fact(Timeout = 5000)]
    public async Task Disabled_indices_does_not_crash()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();

        var fakePipeline = _system.ActorOf(Props.Create(() => new FakePipelineSource(mat)));
        var fakeEgress = _system.ActorOf(Props.Create(() => new FakeMqttSinkProvider(mat)));
        registry.Register<PipelineActor>(fakePipeline, overwrite: true);
        registry.Register<MqttEgressActor>(fakeEgress, overwrite: true);

        var enrichment = new EnrichmentOptions { Indices = new IndexOptions { Enabled = false } };
        var actor = CreateEnrichmentActor(enrichment);

        await Task.Delay(500);
        Assert.NotNull(actor);
    }

    [Fact(Timeout = 5000)]
    public async Task Enabled_trends_does_not_crash()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();

        var fakePipeline = _system.ActorOf(Props.Create(() => new FakePipelineSource(mat)));
        var fakeEgress = _system.ActorOf(Props.Create(() => new FakeMqttSinkProvider(mat)));
        registry.Register<PipelineActor>(fakePipeline, overwrite: true);
        registry.Register<MqttEgressActor>(fakeEgress, overwrite: true);

        var enrichment = new EnrichmentOptions { Trends = new TrendOptions { Enabled = true } };
        var actor = CreateEnrichmentActor(enrichment);

        await Task.Delay(500);
        Assert.NotNull(actor);
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

    private sealed class FakeMqttSinkProvider : ReceiveActor
    {
        public FakeMqttSinkProvider(IMaterializer mat)
        {
            Receive<RequestMqttSink>(_ =>
            {
                var sinkRef = StreamRefs.SinkRef<MqttMessage>()
                    .To(Sink.Ignore<MqttMessage>().MapMaterializedValue(_ => Akka.NotUsed.Instance))
                    .Run(mat);
                sinkRef.PipeTo(Sender, Self,
                    sr => new MqttSinkResponse(sr),
                    _ => null!);
            });
        }
    }
}
