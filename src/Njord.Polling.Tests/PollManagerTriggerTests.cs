using System.Collections.Immutable;
using Akka.Actor;
using Akka.Cluster.Sharding;
using Akka.Hosting;
using Akka.TestKit;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Polling.Tests;

// A probe plays the poll shard region; the test plays the workers by sending their reports.
public sealed class PollManagerTriggerTests : Akka.Hosting.TestKit.TestKit
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

    [Fact(Timeout = 30_000)]
    public async Task Trigger_immediate_poll_for_one_location_reaches_its_worker()
    {
        var ct = TestContext.Current.CancellationToken;
        var manager = await StartedManagerAsync(ct);

        var result = await manager.Ask<TriggerImmediatePollCompleted>(
            new TriggerImmediatePoll(new Location("Lucerne", 0, 0)), TimeSpan.FromSeconds(15), ct);

        var trigger = await _region.ExpectMsgAsync<TriggerLocationPoll>(TimeSpan.FromSeconds(5), cancellationToken: ct);
        Assert.Multiple(
            () => Assert.Equal(1, result.Count),
            () => Assert.Equal(["lucerne"], result.Targets),
            () => Assert.Equal("lucerne", trigger.LocationName));
    }

    [Fact(Timeout = 30_000)]
    public async Task Trigger_immediate_poll_for_all_returns_all_targets()
    {
        var ct = TestContext.Current.CancellationToken;
        var manager = await StartedManagerAsync(ct);

        var result = await manager.Ask<TriggerImmediatePollCompleted>(
            new TriggerImmediatePoll(null), TimeSpan.FromSeconds(15), ct);

        Assert.Multiple(
            () => Assert.Equal(Targets.Length, result.Count),
            () => Assert.Equal(Targets.Select(t => t.ToString()), result.Targets));
        foreach (var target in Targets)
        {
            var trigger = await _region.ExpectMsgAsync<TriggerLocationPoll>(TimeSpan.FromSeconds(5), cancellationToken: ct);
            Assert.Equal(target.Id, trigger.LocationName);
        }
    }

    [Fact(Timeout = 30_000)]
    public async Task Trigger_immediate_poll_for_unknown_location_returns_zero()
    {
        var ct = TestContext.Current.CancellationToken;
        var manager = await StartedManagerAsync(ct);

        var result = await manager.Ask<TriggerImmediatePollCompleted>(
            new TriggerImmediatePoll(new Location("nonexistent", 0, 0)), TimeSpan.FromSeconds(15), ct);

        Assert.Multiple(
            () => Assert.Equal(0, result.Count),
            () => Assert.Empty(result.Targets));
        await _region.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), ct);
    }
}
