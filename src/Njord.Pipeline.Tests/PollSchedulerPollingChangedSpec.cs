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
using Njord.Messages.Common;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;
using Njord.Weather;

namespace Njord.Pipeline.Tests;

public sealed class PollSchedulerPollingChangedSpec : NjordActorSpec
{
    private static readonly TimeSpan LongDiscovery = TimeSpan.FromHours(1);

    private readonly FakeTimeProvider _time = TestTimeouts.AtEpoch();
    private Akka.TestKit.TestProbe _fetchProbe = null!;

    protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        services.AddSingleton<TimeProvider>(_time);
        services.AddSingleton(Options.Create(new NjordOptions
        {
            Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2"],
        }));
        services.AddSingleton(Options.Create(new PollingOptions { DiscoveryInterval = LongDiscovery }));
        services.AddSingleton(new NjordHealthMonitor { ServiceStartedUtc = _time.GetUtcNow() });
    }

    protected override void ConfigureActors(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder
            .WithActors((system, registry) =>
            {
                _fetchProbe = CreateTestProbe();
                var probe = _fetchProbe;
                registry.Register<IForecastGateway>(system.ActorOf(Props.Create(() => new ForwardingGateway(probe))));
            })
            .WithActors((system, registry, resolver) =>
                registry.Register<IPollScheduler>(system.ActorOf(resolver.Props<PollScheduler>(), "scheduler")));
    }

    private IActorRef Scheduler => GetActor<IPollScheduler>();

    private async Task CompleteFirstPollAsync(CancellationToken ct)
    {
        await _fetchProbe.ExpectMsgAsync<FetchForecast>(cancellationToken: ct);
        await Scheduler.Ask<Ack>(new FetchForecastCompleted(new LocationId("lucerne"), "icon_d2", 42), TimeSpan.FromSeconds(9), ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Without_a_change_the_next_discovery_poll_waits_for_the_configured_interval()
    {
        var ct = TestContext.Current.CancellationToken;
        await CompleteFirstPollAsync(ct);

        await _fetchProbe.ExpectNoMsgAsync(TimeSpan.FromSeconds(1), ct);
        var states = await Scheduler.Ask<PollStatesResult>(new QueryPollStates(), TimeSpan.FromSeconds(9), ct);
        Assert.Equal(_time.GetUtcNow() + LongDiscovery, Assert.Single(states.Entries).NextPollUtc);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_shorter_discovery_interval_replans_the_pending_poll()
    {
        var ct = TestContext.Current.CancellationToken;
        await CompleteFirstPollAsync(ct);

        Scheduler.Tell(new PollingChanged(TimeSpan.FromMinutes(60), TimeSpan.FromMilliseconds(100)));

        var next = await _fetchProbe.ExpectMsgAsync<FetchForecast>(cancellationToken: ct);
        Assert.Equal("icon_d2", next.ModelId);
        await _fetchProbe.ExpectNoMsgAsync(TimeSpan.FromSeconds(1), ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_longer_discovery_interval_moves_the_next_poll_out()
    {
        var ct = TestContext.Current.CancellationToken;
        await CompleteFirstPollAsync(ct);

        Scheduler.Tell(new PollingChanged(TimeSpan.FromMinutes(60), TimeSpan.FromHours(2)));

        await AwaitAssertAsync(async () =>
        {
            var states = await Scheduler.Ask<PollStatesResult>(new QueryPollStates(), TimeSpan.FromSeconds(9), ct);
            Assert.Equal(_time.GetUtcNow() + TimeSpan.FromHours(2), Assert.Single(states.Entries).NextPollUtc);
        }, TestTimeouts.AwaitAssertMax, cancellationToken: ct);
    }

    private sealed class ForwardingGateway : ReceiveActor
    {
        public ForwardingGateway(IActorRef probe)
        {
            Receive<FetchForecast>(msg => probe.Tell(msg));
        }
    }
}
