using Akka;
using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Health;
using Njord.Ingest;
using Njord.Pipeline;
using Njord.Tests.Shared;
using Servus.Akka;

namespace Njord.Tests.Pipeline;

public sealed class SchedulerActorSnapshotSpec : Akka.Hosting.TestKit.TestKit
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 7, 12, 6, 0, 0, TimeSpan.Zero));
    private Akka.TestKit.TestProbe _offerProbe = null!;

    protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        var options = new NjordOptions
        {
            DiscoveryInterval = TimeSpan.FromMilliseconds(50),
            Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2"],
        };
        services.AddSingleton<TimeProvider>(_time);
        services.AddSingleton(Options.Create(options));
        services.AddSingleton(ParameterRegistry.Resolve(["Weather"], [], []));
        services.AddSingleton(new NjordHealthState { ServiceStartedUtc = _time.GetUtcNow() });
    }

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder
            .AddTestPersistence()
            .WithActors((system, registry) =>
            {
                _offerProbe = CreateTestProbe();
                var mat = system.Materializer();
                var fakePipeline = system.ActorOf(
                    Props.Create(() => new FakePipelineActor(_offerProbe, mat)));
                registry.Register<PipelineActor>(fakePipeline);
            })
            .WithResolvableActors(r =>
            {
                r.Register<SchedulerActor>("scheduler");
            });
    }

    private IActorRef Scheduler => ActorRegistry.Get<SchedulerActor>();

    [Fact(Timeout = 10000)]
    public async Task State_recovers_from_snapshot_after_restart()
    {
        await _offerProbe.ExpectMsgAsync<WeightedTarget>();

        for (var i = 0; i < 51; i++)
        {
            await Scheduler.Ask<Ack>(new HashResult("lucerne", "icon_d2", 1000 + i), TimeSpan.FromSeconds(2));
        }

        var statesBefore = await Scheduler.Ask<PollStatesSnapshot>(new GetPollStates(), TimeSpan.FromSeconds(2));
        var entryBefore = statesBefore.Entries.Single();
        Assert.Equal(PollPhase.Steady, entryBefore.Phase);

        await Scheduler.GracefulStop(TimeSpan.FromSeconds(3));

        await Task.Delay(200);

        var props = Akka.DependencyInjection.DependencyResolver.For(Sys)
            .Props<SchedulerActor>();
        var recovered = Sys.ActorOf(props, "scheduler");
        ActorRegistry.Register<SchedulerActor>(recovered, overwrite: true);

        await Task.Delay(500);

        var statesAfter = await recovered.Ask<PollStatesSnapshot>(new GetPollStates(), TimeSpan.FromSeconds(2));
        var entryAfter = statesAfter.Entries.Single();
        Assert.Equal(PollPhase.Steady, entryAfter.Phase);
        Assert.NotNull(entryAfter.CycleSeconds);
    }

    private sealed class FakePipelineActor : ReceiveActor
    {
        public FakePipelineActor(IActorRef probe, IMaterializer mat)
        {
            Receive<RequestPipelineSink>(_ =>
            {
                var (hubSink, hubSource) = MergeHub.Source<WeightedTarget>(perProducerBufferSize: 8)
                    .PreMaterialize(mat);

                hubSource
                    .RunWith(Sink.ForEach<WeightedTarget>(t => probe.Tell(t)), mat);

                StreamRefs.SinkRef<WeightedTarget>()
                    .To(hubSink)
                    .Run(mat)
                    .PipeTo(Sender, Self,
                        sr => new PipelineSinkResponse(sr),
                        _ => null!);
            });

            Receive<RequestPipelineSource>(_ =>
            {
                Source.Empty<FetchOutcome>()
                    .RunWith(StreamRefs.SourceRef<FetchOutcome>(), mat)
                    .PipeTo(Sender, Self,
                        sr => new PipelineSourceResponse(sr),
                        _ => null!);
            });
        }
    }
}
