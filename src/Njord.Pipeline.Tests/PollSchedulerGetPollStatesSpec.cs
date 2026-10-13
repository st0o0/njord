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

public sealed class PollSchedulerGetPollStatesSpec : NjordActorSpec
{
    private readonly FakeTimeProvider _time = TestTimeouts.AtEpoch();
    private Akka.TestKit.TestProbe _offerProbe = null!;

    protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        var options = new NjordOptions
        {
            Locations =
            [
                new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 },
                new LocationOptions { Name = "zurich", Latitude = 47.37, Longitude = 8.54 },
            ],
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
    public async Task Get_poll_states_returns_all_configured_models()
    {
        await _offerProbe.ExpectMsgAsync<FetchForecast>(cancellationToken: TestContext.Current.CancellationToken);
        await _offerProbe.ExpectMsgAsync<FetchForecast>(cancellationToken: TestContext.Current.CancellationToken);

        var snapshot = await Scheduler.Ask<PollStatesResult>(
            new QueryPollStates(), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Equal(2, snapshot.Entries.Count);
        Assert.Contains(snapshot.Entries, e => e is { Location.Name: "lucerne", ModelId: "icon_d2" });
        Assert.Contains(snapshot.Entries, e => e is { Location.Name: "zurich", ModelId: "icon_d2" });
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Get_poll_states_reflects_discovery_phase_initially()
    {
        await _offerProbe.ExpectMsgAsync<FetchForecast>(cancellationToken: TestContext.Current.CancellationToken);
        await _offerProbe.ExpectMsgAsync<FetchForecast>(cancellationToken: TestContext.Current.CancellationToken);

        var snapshot = await Scheduler.Ask<PollStatesResult>(
            new QueryPollStates(), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        var entry = snapshot.Entries.First(e => e.Location.Name == "lucerne");
        Assert.Equal(PollPhase.Discovery, entry.Phase);
        Assert.Null(entry.CycleSeconds);
        Assert.Null(entry.LastChangeUtc);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Get_poll_states_reflects_state_after_hash_change()
    {
        await _offerProbe.ExpectMsgAsync<FetchForecast>(cancellationToken: TestContext.Current.CancellationToken);
        await _offerProbe.ExpectMsgAsync<FetchForecast>(cancellationToken: TestContext.Current.CancellationToken);

        await Scheduler.Ask<Ack>(new FetchForecastCompleted(new LocationId("lucerne"), "icon_d2", 42), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        var snapshot = await Scheduler.Ask<PollStatesResult>(
            new QueryPollStates(), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        var entry = snapshot.Entries.First(e => e.Location.Name == "lucerne");
        Assert.Equal(0, entry.MissCount);
        Assert.NotNull(entry.LastChangeUtc);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Get_poll_states_reflects_miss_count_after_unchanged_hash()
    {
        await _offerProbe.ExpectMsgAsync<FetchForecast>(cancellationToken: TestContext.Current.CancellationToken);
        await _offerProbe.ExpectMsgAsync<FetchForecast>(cancellationToken: TestContext.Current.CancellationToken);

        await Scheduler.Ask<Ack>(new FetchForecastCompleted(new LocationId("lucerne"), "icon_d2", 42), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        await Scheduler.Ask<Ack>(new FetchForecastCompleted(new LocationId("lucerne"), "icon_d2", 42), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        var snapshot = await Scheduler.Ask<PollStatesResult>(
            new QueryPollStates(), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        var entry = snapshot.Entries.First(e => e.Location.Name == "lucerne");
        Assert.Equal(1, entry.MissCount);
    }

    private sealed class FakeGatewayActor : ReceiveActor
    {
        public FakeGatewayActor(IActorRef probe)
        {
            Receive<FetchForecast>(msg => probe.Tell(msg));
        }
    }
}
