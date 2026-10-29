using System.Collections.Immutable;
using Akka.Actor;
using Akka.Cluster.Sharding;
using Akka.Hosting;
using Akka.TestKit;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Messages.Polling;
using Njord.Tests.Shared;

namespace Njord.Polling.Tests;

// A probe plays the poll shard region; the test plays the workers by sending their reports.
public sealed class PollManagerStartupTests : Akka.Hosting.TestKit.TestKit
{
    private static readonly TimeSpan Discovery = TimeSpan.FromMinutes(20);
    private readonly FakeTimeProvider _time = new(TestTimeConstants.PipelineEpoch);
    private TestProbe _region = null!;

    private readonly IOptions<NjordOptions> _njordOptions = Options.Create(new NjordOptions
    {
        Locations =
        [
            new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 },
            new LocationOptions { Name = "zurich", Latitude = 47.37, Longitude = 8.54 },
        ],
    });

    private readonly IOptions<PollingOptions> _pollingOptions =
        Options.Create(AppSettingsDefaults.Options<PollingOptions>(o => o.DiscoveryInterval = Discovery));

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.AddTestPersistence();
        builder.WithActors((_, registry) =>
        {
            _region = CreateTestProbe("region");
            registry.Register<IPollRegion>(_region.Ref);
        });
    }

    private static readonly PollTarget[] Targets =
    [
        new(new Location("lucerne", 0, 0)),
        new(new Location("zurich", 0, 0)),
    ];

    private IActorRef CreateManager() =>
        Sys.ActorOf(Props.Create(() => new PollManager(_njordOptions, _pollingOptions, _time)), $"manager-{Guid.NewGuid():N}");

    // The manager starts every target, then asks the region which entities run.
    private async Task<IActorRef> StartedManagerAsync(CancellationToken ct, params string[] runningEntities)
    {
        var manager = CreateManager();
        foreach (var target in Targets)
        {
            var start = await _region.ExpectMsgAsync<StartPolling>(TimeSpan.FromSeconds(5), cancellationToken: ct);
            Assert.Equal(new StartPolling(target, Discovery), start);
        }

        await _region.ExpectMsgAsync<GetShardRegionState>(TimeSpan.FromSeconds(5), cancellationToken: ct);
        _region.LastSender.Tell(new CurrentShardRegionState(
            ImmutableHashSet.Create(new ShardState("0", [.. Targets.Select(t => t.Id), .. runningEntities])),
            ImmutableHashSet<string>.Empty));
        return manager;
    }

    private PollStateEntry Entry(PollTarget target, PollPhase phase = PollPhase.Discovery, int misses = 0,
        DateTimeOffset? lastChange = null, long? cycle = null) =>
        new(target.Location, phase, _time.GetUtcNow() + Discovery, lastChange, misses, cycle);

    private static async Task<PollStatesResult> QueryAsync(IActorRef manager, CancellationToken ct) =>
        await manager.Ask<PollStatesResult>(new QueryPollStates(), TimeSpan.FromSeconds(15), ct);

    [Fact(Timeout = 30_000)]
    public async Task Every_resolved_model_of_every_location_is_started_once()
    {
        // StartedManagerAsync asserts the exact StartPolling sequence: global models first (the duplicate
        // spelling collapsed), then the location's own models.
        await StartedManagerAsync(TestContext.Current.CancellationToken);

        await _region.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_running_worker_of_a_removed_target_is_stopped()
    {
        var ct = TestContext.Current.CancellationToken;
        await StartedManagerAsync(ct, "atlantis");

        var stop = await _region.ExpectMsgAsync<StopPolling>(TimeSpan.FromSeconds(5), cancellationToken: ct);

        Assert.Equal("atlantis", stop.LocationName);
        await _region.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), ct);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_terminated_worker_is_started_again()
    {
        var ct = TestContext.Current.CancellationToken;
        var manager = await StartedManagerAsync(ct);
        var worker = CreateTestProbe("worker");
        manager.Tell(new PollWorkerReport(Targets[1], PollWorkerOutcome.Planned, Entry(Targets[1])), worker.Ref);
        await QueryAsync(manager, ct);

        await worker.Ref.GracefulStop(TimeSpan.FromSeconds(15));

        var start = await _region.ExpectMsgAsync<StartPolling>(TimeSpan.FromSeconds(5), cancellationToken: ct);
        Assert.Equal(new StartPolling(Targets[1], Discovery), start);
    }
}
