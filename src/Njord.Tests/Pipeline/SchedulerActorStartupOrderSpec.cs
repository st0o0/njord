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
using Njord.Pipeline;
using Njord.Tests.Shared;
using Servus.Akka;

namespace Njord.Tests.Pipeline;

public sealed class SchedulerActorStartupOrderSpec : Akka.Hosting.TestKit.TestKit
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 7, 12, 6, 0, 0, TimeSpan.Zero));

    protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        var options = new NjordOptions
        {
            DiscoveryInterval = TimeSpan.FromMilliseconds(50),
            Locations =
            [
                new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 },
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
            .WithResolvableActors(r =>
            {
                r.Register<SchedulerActor>("scheduler");
            })
            .WithActors((system, registry) =>
            {
                var mat = system.Materializer();
                var fakePipeline = system.ActorOf(
                    Props.Create(() => new FakePipelineActor(mat)));
                registry.Register<PipelineActor>(fakePipeline);
            });
    }

    private IActorRef Scheduler => ActorRegistry.Get<SchedulerActor>();

    [Fact(Timeout = 5000)]
    public async Task Scheduler_starts_when_pipeline_registered_after()
    {
        var snapshot = await Scheduler.Ask<PollStatesSnapshot>(
            new GetPollStates(), TimeSpan.FromSeconds(3));

        Assert.NotNull(snapshot);
    }

    [Fact(Timeout = 5000)]
    public async Task Scheduler_reaches_ready_and_initializes_states()
    {
        var scheduler = Scheduler;

        await AwaitConditionAsync(async () =>
        {
            var snapshot = await scheduler.Ask<PollStatesSnapshot>(
                new GetPollStates(), TimeSpan.FromSeconds(1));
            return snapshot.Entries.Count > 0;
        }, TimeSpan.FromSeconds(3));
    }

    private sealed class FakePipelineActor : ReceiveActor
    {
        public FakePipelineActor(IMaterializer mat)
        {
            Receive<RequestPipelineSink>(_ =>
            {
                var (hubSink, hubSource) = MergeHub.Source<WeightedTarget>(perProducerBufferSize: 8)
                    .PreMaterialize(mat);

                hubSource.RunWith(Sink.Ignore<WeightedTarget>(), mat);

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
