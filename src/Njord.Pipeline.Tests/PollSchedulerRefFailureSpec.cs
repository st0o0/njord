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

public sealed class PollSchedulerRefFailureSpec : NjordActorSpec
{
    private readonly FakeTimeProvider _time = TestTimeouts.AtEpoch();
    private Akka.TestKit.TestProbe _offerProbe = null!;

    protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
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
    public async Task Recovers_after_gateway_termination_with_new_ref()
    {
        var ct = TestContext.Current.CancellationToken;
        await _offerProbe.ExpectMsgAsync<FetchForecast>(cancellationToken: ct);

        var oldGateway = ActorRegistry.Get<IForecastGateway>();
        await WatchAsync(oldGateway);
        await oldGateway.GracefulStop(TimeSpan.FromSeconds(2));
        await ExpectTerminatedAsync(oldGateway, cancellationToken: ct);

        var newGateway = Sys.ActorOf(
            Props.Create(() => new FakeGatewayActor(_offerProbe)), "gateway-replacement");
        ActorRegistry.Register<IForecastGateway>(newGateway, overwrite: true);

        var msg = await _offerProbe.ExpectMsgAsync<FetchForecast>(cancellationToken: ct);
        Assert.Equal("lucerne", msg.Location);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Stays_responsive_after_gateway_termination()
    {
        var ct = TestContext.Current.CancellationToken;
        await _offerProbe.ExpectMsgAsync<FetchForecast>(cancellationToken: ct);

        var gateway = ActorRegistry.Get<IForecastGateway>();
        await WatchAsync(gateway);
        await gateway.GracefulStop(TimeSpan.FromSeconds(2));
        await ExpectTerminatedAsync(gateway, cancellationToken: ct);

        var states = await Scheduler.Ask<PollStatesResponse>(
            new QueryPollStates(), TimeSpan.FromSeconds(2), ct);

        Assert.IsAssignableFrom<PollStatesResponse>(states);
    }

    private sealed class FakeGatewayActor : ReceiveActor
    {
        public FakeGatewayActor(IActorRef probe)
        {
            Receive<FetchForecast>(msg => probe.Tell(msg));
        }
    }
}
