using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Health;
using Njord.Domain.Options;
using Njord.Domain.Weather;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Pipeline.Tests;

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
            .WithActors((system, registry, resolver) =>
            {
                registry.Register<ISchedulerActor>(
                    system.ActorOf(resolver.Props<SchedulerActor>(), "scheduler"));
            })
            .WithActors((system, registry) =>
            {
                var fakePipeline = system.ActorOf(
                    Props.Create(() => new FakePipelineActor()));
                registry.Register<IPipelineActor>(fakePipeline);
            })
            .AddTestTimefactor();
    }

    private IActorRef Scheduler => ActorRegistry.Get<ISchedulerActor>();

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Scheduler_starts_when_pipeline_registered_after()
    {
        var snapshot = await Scheduler.Ask<QueryPollStatesResult>(
            new QueryPollStates(), TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

        Assert.NotNull(snapshot);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Scheduler_reaches_ready_and_initializes_states()
    {
        var scheduler = Scheduler;

        await AwaitConditionAsync(async () =>
        {
            var snapshot = await scheduler.Ask<QueryPollStatesResult>(
                new QueryPollStates(), TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            return snapshot.Entries.Count > 0;
        }, TestTimeouts.AwaitAssertMax, TestContext.Current.CancellationToken);
    }

    private sealed class FakePipelineActor : ReceiveActor
    {
        public FakePipelineActor()
        {
            Receive<WeightedTarget>(_ => { });
        }
    }
}
