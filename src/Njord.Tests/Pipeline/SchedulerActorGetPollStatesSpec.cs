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

public sealed class SchedulerActorGetPollStatesSpec : Akka.Hosting.TestKit.TestKit
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 7, 12, 6, 0, 0, TimeSpan.Zero));
    private Akka.TestKit.TestProbe _offerProbe = null!;

    protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        var options = new NjordOptions
        {
            DiscoveryInterval = TimeSpan.FromMilliseconds(50),
            Locations =
            [
                new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 },
                new LocationOptions { Name = "zurich", Latitude = 47.37, Longitude = 8.54 },
            ],
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

    [Fact(Timeout = 5000)]
    public async Task Get_poll_states_returns_all_configured_models()
    {
        await _offerProbe.ExpectMsgAsync<WeightedTarget>();
        await _offerProbe.ExpectMsgAsync<WeightedTarget>();

        var snapshot = await Scheduler.Ask<PollStatesSnapshot>(
            new GetPollStates(), TimeSpan.FromSeconds(2));

        Assert.Equal(2, snapshot.Entries.Count);
        Assert.Contains(snapshot.Entries, e => e.Location == "lucerne" && e.ModelId == "icon_d2");
        Assert.Contains(snapshot.Entries, e => e.Location == "zurich" && e.ModelId == "icon_d2");
    }

    [Fact(Timeout = 5000)]
    public async Task Get_poll_states_reflects_discovery_phase_initially()
    {
        await _offerProbe.ExpectMsgAsync<WeightedTarget>();
        await _offerProbe.ExpectMsgAsync<WeightedTarget>();

        var snapshot = await Scheduler.Ask<PollStatesSnapshot>(
            new GetPollStates(), TimeSpan.FromSeconds(2));

        var entry = snapshot.Entries.First(e => e.Location == "lucerne");
        Assert.Equal(PollPhase.Discovery, entry.Phase);
        Assert.Null(entry.CycleSeconds);
        Assert.Null(entry.LastChangeUtc);
    }

    [Fact(Timeout = 5000)]
    public async Task Get_poll_states_reflects_state_after_hash_change()
    {
        await _offerProbe.ExpectMsgAsync<WeightedTarget>();
        await _offerProbe.ExpectMsgAsync<WeightedTarget>();

        await Scheduler.Ask<Ack>(new HashResult("lucerne", "icon_d2", 42), TimeSpan.FromSeconds(2));

        var snapshot = await Scheduler.Ask<PollStatesSnapshot>(
            new GetPollStates(), TimeSpan.FromSeconds(2));

        var entry = snapshot.Entries.First(e => e.Location == "lucerne");
        Assert.Equal(0, entry.MissCount);
        Assert.NotNull(entry.LastChangeUtc);
    }

    [Fact(Timeout = 5000)]
    public async Task Get_poll_states_reflects_miss_count_after_unchanged_hash()
    {
        await _offerProbe.ExpectMsgAsync<WeightedTarget>();
        await _offerProbe.ExpectMsgAsync<WeightedTarget>();

        await Scheduler.Ask<Ack>(new HashResult("lucerne", "icon_d2", 42), TimeSpan.FromSeconds(2));
        await Scheduler.Ask<Ack>(new HashResult("lucerne", "icon_d2", 42), TimeSpan.FromSeconds(2));

        var snapshot = await Scheduler.Ask<PollStatesSnapshot>(
            new GetPollStates(), TimeSpan.FromSeconds(2));

        var entry = snapshot.Entries.First(e => e.Location == "lucerne");
        Assert.Equal(1, entry.MissCount);
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
