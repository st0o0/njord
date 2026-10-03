using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Time.Testing;
using Njord.Actors;
using Njord.Configuration;
using Njord.Domain.Analysis;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Enrichment;
using Njord.Messages.Egress;
using Njord.Messages.Pipeline;
using Njord.Pipeline;
using Njord.Tests.Actors;
using Njord.Tests.Shared;

namespace Njord.Tests.Enrichment;

[Collection("EnrichmentActor")]
public sealed class EnrichmentActorSpec : Akka.Hosting.TestKit.TestKit
{
    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder
            .WithActors((system, registry) =>
            {
                var mat = system.Materializer();
                var fakePipeline = system.ActorOf(Props.Create(() => new FakePipelineSource(mat)));
                var fakeEgress = system.ActorOf(Props.Create(() => new FakeEgressSinkProvider(mat)));
                registry.Register<IPipelineActor>(fakePipeline);
                registry.Register<IEgressActor>(fakeEgress);
            })
            .AddTestTimefactor();
    }

    private static NjordOptions DefaultOptions() => new()
    {
        Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
        Models = ["icon_d2"],
    };

    private IActorRef CreateEnrichmentActor(
        EnrichmentOptions? enrichment = null)
    {
        var options = DefaultOptions();
        if (enrichment is not null)
            options.Enrichment = enrichment;
        var optionsWrapped = Microsoft.Extensions.Options.Options.Create(options);
        var parameters = ParameterRegistry.Resolve(["Weather"], [], []);

        IEnumerable<IEnrichmentFeature> features = [];
        var consensusFactory = new ConsensusSnapshotFactory(parameters,
            new FakeTimeProvider(new DateTimeOffset(2026, 7, 12, 6, 0, 0, TimeSpan.Zero)));

        return Sys.ActorOf(Props.Create(() => new EnrichmentActor(
            optionsWrapped,
            consensusFactory,
            features)));
    }

    private async Task AssertActorAlive(IActorRef actor, CancellationToken cancellationToken)
    {
        var identity = await actor.Ask<ActorIdentity>(new Identify(42), TimeSpan.FromSeconds(3), cancellationToken);
        Assert.Equal(42, identity.MessageId);
    }

    [Fact(Timeout = 5000)]
    public async Task Requests_source_ref_from_pipeline_actor_on_startup()
    {
        var actor = CreateEnrichmentActor();

        await AssertActorAlive(actor, TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 5000)]
    public async Task Re_requests_pipeline_source_after_pipeline_source_failure()
    {
        var ct = TestContext.Current.CancellationToken;
        var requestProbe = CreateTestProbe();
        ActorRegistry.Register<IPipelineActor>(Sys.ActorOf(FailingRefProvider.Props(requestProbe)), overwrite: true);

        CreateEnrichmentActor();

        await requestProbe.ExpectMsgAsync<RequestPipelineSource>(cancellationToken: ct);
        await requestProbe.ExpectMsgAsync<RequestPipelineSource>(TimeSpan.FromSeconds(4), cancellationToken: ct);
    }

    [Fact(Timeout = 5000)]
    public async Task Re_requests_egress_sink_after_egress_sink_failure()
    {
        var ct = TestContext.Current.CancellationToken;
        var requestProbe = CreateTestProbe();
        ActorRegistry.Register<IEgressActor>(Sys.ActorOf(FailingRefProvider.Props(requestProbe)), overwrite: true);

        CreateEnrichmentActor();

        await requestProbe.ExpectMsgAsync<RequestEgressSink>(cancellationToken: ct);
        await requestProbe.ExpectMsgAsync<RequestEgressSink>(TimeSpan.FromSeconds(4), cancellationToken: ct);
    }

    [Fact(Timeout = 5000)]
    public async Task Disabled_consensus_does_not_crash()
    {
        var enrichment = new EnrichmentOptions { Consensus = new ConsensusOptions { Enabled = false } };
        var actor = CreateEnrichmentActor(enrichment);

        await AssertActorAlive(actor, TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 5000)]
    public async Task Disabled_derived_does_not_crash()
    {
        var enrichment = new EnrichmentOptions { Derived = new DerivedOptions { Enabled = false } };
        var actor = CreateEnrichmentActor(enrichment);

        await AssertActorAlive(actor, TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 5000)]
    public async Task All_consumers_enabled_does_not_crash()
    {
        var enrichment = new EnrichmentOptions
        {
            Consensus = new ConsensusOptions { Enabled = true },
            Alerts = new AlertOptions { Enabled = true },
            Derived = new DerivedOptions { Enabled = true },
        };
        var actor = CreateEnrichmentActor(enrichment);

        await AssertActorAlive(actor, TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 5000)]
    public async Task Disabled_trends_does_not_crash()
    {
        var enrichment = new EnrichmentOptions { Trends = new TrendOptions { Enabled = false } };
        var actor = CreateEnrichmentActor(enrichment);

        await AssertActorAlive(actor, TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 5000)]
    public async Task Disabled_history_does_not_crash()
    {
        var enrichment = new EnrichmentOptions { History = new HistoryOptions { Enabled = false } };
        var actor = CreateEnrichmentActor(enrichment);

        await AssertActorAlive(actor, TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 5000)]
    public async Task Disabled_indices_does_not_crash()
    {
        var enrichment = new EnrichmentOptions { Indices = new IndexOptions { Enabled = false } };
        var actor = CreateEnrichmentActor(enrichment);

        await AssertActorAlive(actor, TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 5000)]
    public async Task Enabled_trends_does_not_crash()
    {
        var enrichment = new EnrichmentOptions { Trends = new TrendOptions { Enabled = true } };
        var actor = CreateEnrichmentActor(enrichment);

        await AssertActorAlive(actor, TestContext.Current.CancellationToken);
    }

    private sealed class FakePipelineSource : ReceiveActor
    {
        public FakePipelineSource(IMaterializer mat)
        {
            Receive<RequestPipelineSource>(msg =>
            {
                var task = Source.Empty<FetchOutcome>()
                    .RunWith(StreamRefs.SourceRef<FetchOutcome>(), mat);
                task.PipeTo(Sender, Self,
                    sr => new PipelineSourceResponse(msg.RequestId, sr),
                    _ => null!);
            });
        }
    }

    private sealed class FakeEgressSinkProvider : ReceiveActor
    {
        public FakeEgressSinkProvider(IMaterializer mat)
        {
            Receive<RequestEgressSink>(msg =>
            {
                var sinkRef = StreamRefs.SinkRef<EgressEvent>()
                    .To(Sink.Ignore<EgressEvent>().MapMaterializedValue(_ => Akka.NotUsed.Instance))
                    .Run(mat);
                sinkRef.PipeTo(Sender, Self,
                    sr => new EgressSinkResponse(msg.RequestId, sr),
                    _ => null!);
            });
        }
    }
}
