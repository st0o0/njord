using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;
using Njord.Core.Health;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;
using Njord.Weather;

namespace Njord.Pipeline.Tests;

public sealed class PollSchedulerSnapshotSpec : NjordActorSpec
{
    private readonly FakeTimeProvider _time = TestTimeouts.AtEpoch();
    private Akka.TestKit.TestProbe _offerProbe = null!;

    protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        services.AddSingleton(FetchTimeouts.Default);
        var options = new NjordOptions
        {
            Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2"],
        };
        services.AddSingleton<TimeProvider>(_time);
        services.AddSingleton(Options.Create(options));
        services.AddSingleton(Options.Create(new PollingOptions { DiscoveryInterval = TimeSpan.FromMilliseconds(50) }));
        services.AddSingleton(new NjordHealthMonitor { ServiceStartedUtc = _time.GetUtcNow() });
    }

    protected override void ConfigureActors(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder
            .WithActors((system, registry) =>
            {
                _offerProbe = CreateTestProbe();
                var fakeGateway = system.ActorOf(
                    Props.Create(() => new FakeGatewayActor(_offerProbe)));
                registry.Register<IForecastGateway>(fakeGateway);
            })
            .WithActors((system, registry, resolver) =>
            {
                registry.Register<IPollScheduler>(
                    system.ActorOf(resolver.Props<PollScheduler>(), "scheduler"));
            });
    }

    private IActorRef Scheduler => GetActor<IPollScheduler>();

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task State_recovers_from_snapshot_after_restart()
    {
        await _offerProbe.ExpectMsgAsync<FetchForecast>(cancellationToken: TestContext.Current.CancellationToken);

        for (var i = 0; i < 51; i++)
        {
            Scheduler.Tell(new FetchForecastCompleted(new LocationId("lucerne"), "icon_d2", 1000 + i));
            await Scheduler.Ask<PollStatesResult>(new QueryPollStates(), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        }

        var statesBefore = await Scheduler.Ask<PollStatesResult>(new QueryPollStates(), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        var entryBefore = statesBefore.Entries.Single();
        Assert.Equal(PollPhase.Steady, entryBefore.Phase);

        await WatchAsync(Scheduler);
        await Scheduler.GracefulStop(TimeSpan.FromSeconds(3));
        await ExpectTerminatedAsync(Scheduler, cancellationToken: TestContext.Current.CancellationToken);

        var props = Akka.DependencyInjection.DependencyResolver.For(Sys)
            .Props<PollScheduler>();
        var recovered = Sys.ActorOf(props, "scheduler");
        ActorRegistry.Register<IPollScheduler>(recovered, overwrite: true);

        var statesAfter = await recovered.Ask<PollStatesResult>(new QueryPollStates(), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        var entryAfter = statesAfter.Entries.Single();
        Assert.Equal(PollPhase.Steady, entryAfter.Phase);
        Assert.NotNull(entryAfter.CycleSeconds);
    }

    private sealed class FakeGatewayActor : ReceiveActor
    {
        public FakeGatewayActor(IActorRef probe)
        {
            Receive<FetchForecast>(msg => probe.Tell(msg));
        }
    }
}
