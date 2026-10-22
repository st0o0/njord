using System.Collections.Immutable;
using Akka.Actor;
using Akka.Cluster.Sharding;
using Akka.DependencyInjection;
using Akka.Hosting;
using Akka.TestKit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Forecast;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Polling.Tests;
// A probe plays the poll shard region; the spec plays the workers by sending their reports.
public abstract class PollManagerSpecBase : NjordActorSpec
{
    protected static readonly TimeSpan Discovery = TimeSpan.FromMinutes(20);

    protected readonly FakeTimeProvider _time = TestTimeouts.AtEpoch();
    protected TestProbe _region = null!;

    protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        services.AddSingleton<TimeProvider>(_time);
        services.AddSingleton(Options.Create(new NjordOptions
        {
            Locations =
            [
                new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 },
                new LocationOptions { Name = "zurich", Latitude = 47.37, Longitude = 8.54 },
            ],
        }));
        services.AddSingleton(Options.Create(AppSettingsDefaults.Options<PollingOptions>(o => o.DiscoveryInterval = Discovery)));
    }

    protected override void ConfigureActors(AkkaConfigurationBuilder builder, IServiceProvider provider) =>
        builder.WithActors((_, registry) =>
        {
            _region = CreateTestProbe("region");
            registry.Register<IPollRegion>(_region.Ref);
        });

    protected static readonly PollTarget[] Targets =
    [
        new(new Location("lucerne", 0, 0)),
        new(new Location("zurich", 0, 0)),
    ];

    protected IActorRef CreateManager() =>
        Sys.ActorOf(DependencyResolver.For(Sys).Props<PollManager>(), $"manager-{Guid.NewGuid():N}");

    // The manager starts every target, then asks the region which entities run.
    protected async Task<IActorRef> StartedManagerAsync(CancellationToken ct, params string[] runningEntities)
    {
        var manager = CreateManager();
        foreach (var target in Targets)
        {
            var start = await _region.ExpectMsgAsync<StartPolling>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
            Assert.Equal(new StartPolling(target, Discovery), start);
        }

        await _region.ExpectMsgAsync<GetShardRegionState>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        _region.LastSender.Tell(new CurrentShardRegionState(
            ImmutableHashSet.Create(new ShardState("0", [.. Targets.Select(t => t.Id), .. runningEntities])),
            ImmutableHashSet<string>.Empty));
        return manager;
    }

    protected PollStateEntry Entry(PollTarget target, PollPhase phase = PollPhase.Discovery, int misses = 0,
        DateTimeOffset? lastChange = null, long? cycle = null) =>
        new(target.Location, phase, _time.GetUtcNow() + Discovery, lastChange, misses, cycle);

    protected static async Task<PollStatesResult> QueryAsync(IActorRef manager, CancellationToken ct) =>
        await manager.Ask<PollStatesResult>(new QueryPollStates(), TestTimeouts.RealTimeWaitMax, ct);
}
