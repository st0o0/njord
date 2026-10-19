using Akka.Actor;
using Akka.Cluster;
using Akka.Cluster.Hosting;
using Akka.Hosting;
using Akka.Remote.Hosting;
using Akka.TestKit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Pipeline.Configuration;
using Njord.Tests.Shared;

namespace Njord.Pipeline.Tests;

// The production polling registration in a real single-node cluster: shard region, manager singleton and
// persistent workers (in-memory journal). The spec only plays the gateway.
public sealed class PollingClusterSpec : NjordActorSpec
{
    private static readonly string[] Targets = ["lucerne", "zurich"];

    private readonly FakeTimeProvider _time = TestTimeouts.AtEpoch();
    private TestProbe _requests = null!;

    protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        services.AddSingleton<TimeProvider>(_time);
        services.AddSingleton(FetchTimeouts.Default);
        services.AddSingleton(Options.Create(new NjordOptions
        {
            Locations =
            [
                new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 },
                new LocationOptions { Name = "zurich", Latitude = 47.37, Longitude = 8.54 },
            ],
        }));
        services.AddSingleton(TestConfigView.Create());
    }

    protected override void ConfigureActors(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder
            .WithRemoting(new RemoteOptions { HostName = "localhost", Port = 0 })
            .WithClustering(new ClusterOptions())
            .WithActors((system, registry) =>
            {
                _requests = CreateTestProbe("requests");
                registry.Register<IForecastGateway>(system.ActorOf(Props.Create(() => new AnsweringGateway(_requests.Ref)), "gateway"));
            })
            .WithPolling()
            .AddStartup((system, _) =>
            {
                var cluster = Cluster.Get(system);
                cluster.Join(cluster.SelfAddress);
            });
    }

    private const int DomainsPerTarget = 3;

    private async Task<IReadOnlySet<string>> ReceiveRequestsAsync(int count, CancellationToken ct)
    {
        var received = new HashSet<string>();
        for (var i = 0; i < count * DomainsPerTarget; i++)
        {
            var request = await _requests.ExpectMsgAsync<FetchDomain>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
            received.Add(request.Location.Name);
        }

        return received;
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Every_configured_target_is_fetched_and_reported()
    {
        var ct = TestContext.Current.CancellationToken;

        var requested = await ReceiveRequestsAsync(Targets.Length, ct);

        Assert.Equal(Targets.ToHashSet(), requested);
        var manager = await ActorRegistry.GetAsync<IPollManager>(ct);
        await AwaitAssertAsync(async () =>
        {
            var states = await manager.Ask<PollStatesResult>(new QueryPollStates(), TestTimeouts.RealTimeWaitMax, ct);
            Assert.Equal(Targets, states.Entries.Select(e => e.Location.Name));
            Assert.All(states.Entries, e => Assert.Equal(_time.GetUtcNow(), e.LastChangeUtc));
        }, TestTimeouts.AwaitAssertMax, TimeSpan.FromMilliseconds(100), cancellationToken: ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_trigger_reaches_the_gateway_through_the_region()
    {
        var ct = TestContext.Current.CancellationToken;
        await ReceiveRequestsAsync(Targets.Length, ct);
        var manager = await ActorRegistry.GetAsync<IPollManager>(ct);
        await AwaitAssertAsync(async () =>
        {
            var states = await manager.Ask<PollStatesResult>(new QueryPollStates(), TestTimeouts.RealTimeWaitMax, ct);
            Assert.All(states.Entries, e => Assert.NotNull(e.LastChangeUtc));
        }, TestTimeouts.AwaitAssertMax, TimeSpan.FromMilliseconds(100), cancellationToken: ct);

        var result = await manager.Ask<TriggerImmediatePollCompleted>(
            new TriggerImmediatePoll(new Location("zurich", 0, 0)), TestTimeouts.RealTimeWaitMax, ct);

        var request = await _requests.ExpectMsgAsync<FetchDomain>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        Assert.Multiple(
            () => Assert.Equal(["zurich"], result.Targets),
            () => Assert.Equal("zurich", request.Location.Name));
    }

    private sealed class AnsweringGateway : ReceiveActor
    {
        public AnsweringGateway(IActorRef requests)
        {
            Receive<FetchDomain>(msg =>
            {
                requests.Tell(msg);
                Sender.Tell(new FetchDomainCompleted(msg.Location, msg.Domain, HashCode.Combine(msg.Location, msg.Domain)));
            });
        }
    }
}
