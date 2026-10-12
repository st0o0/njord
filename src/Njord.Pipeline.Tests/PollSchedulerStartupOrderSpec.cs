using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Health;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;
using Njord.Weather;

namespace Njord.Pipeline.Tests;

public sealed class PollSchedulerStartupOrderSpec : NjordActorSpec
{
    private readonly FakeTimeProvider _time = TestTimeouts.AtEpoch();

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
        services.AddSingleton(new NjordHealthState { ServiceStartedUtc = _time.GetUtcNow() });
    }

    protected override void ConfigureActors(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder
            .WithActors((system, registry, resolver) =>
            {
                registry.Register<IPollScheduler>(
                    system.ActorOf(resolver.Props<PollScheduler>(), "scheduler"));
            })
            .WithActors((system, registry) =>
            {
                var fakeGateway = system.ActorOf(
                    Props.Create(() => new FakeGatewayActor()));
                registry.Register<IForecastGateway>(fakeGateway);
            });
    }

    private IActorRef Scheduler => GetActor<IPollScheduler>();

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Scheduler_starts_when_gateway_registered_after()
    {
        var snapshot = await Scheduler.Ask<PollStatesResult>(
            new QueryPollStates(), TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

        Assert.NotNull(snapshot);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Scheduler_reaches_ready_and_initializes_states()
    {
        var scheduler = Scheduler;

        await AwaitConditionAsync(async () =>
        {
            var snapshot = await scheduler.Ask<PollStatesResult>(
                new QueryPollStates(), TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            return snapshot.Entries.Count > 0;
        }, TestTimeouts.AwaitAssertMax, TestContext.Current.CancellationToken);
    }

    private sealed class FakeGatewayActor : ReceiveActor
    {
        public FakeGatewayActor()
        {
            Receive<FetchForecast>(_ => { });
        }
    }
}
