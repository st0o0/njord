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
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Tests.Shared;

namespace Njord.Pipeline.Tests;

// A probe plays the poll shard region; the spec plays the workers by sending their reports.
public sealed class PollManagerSpec : NjordActorSpec
{
    private static readonly TimeSpan Discovery = TimeSpan.FromMinutes(20);

    private readonly FakeTimeProvider _time = TestTimeouts.AtEpoch();
    private TestProbe _region = null!;

    protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        services.AddSingleton<TimeProvider>(_time);
        services.AddSingleton(Options.Create(new NjordOptions
        {
            Locations =
            [
                new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31, Models = ["meteoswiss_icon_ch1"] },
                new LocationOptions { Name = "zurich", Latitude = 47.37, Longitude = 8.54 },
            ],
            Models = ["icon_d2", "ICON_D2", "gfs_seamless"],
        }));
        services.AddSingleton(TestConfigView.Create(polling: AppSettingsDefaults.Options<PollingOptions>(o => o.DiscoveryInterval = Discovery)));
    }

    protected override void ConfigureActors(AkkaConfigurationBuilder builder, IServiceProvider provider) =>
        builder.WithActors((_, registry) =>
        {
            _region = CreateTestProbe("region");
            registry.Register<IPollRegion>(_region.Ref);
        });

    private static readonly PollTarget[] Targets =
    [
        new(new Location("lucerne", 0, 0)),
        new(new Location("zurich", 0, 0)),
    ];

    private IActorRef CreateManager() =>
        Sys.ActorOf(DependencyResolver.For(Sys).Props<PollManager>(), $"manager-{Guid.NewGuid():N}");

    // The manager starts every target, then asks the region which entities run.
    private async Task<IActorRef> StartedManagerAsync(CancellationToken ct, params string[] runningEntities)
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

    private PollStateEntry Entry(PollTarget target, PollPhase phase = PollPhase.Discovery, int misses = 0,
        DateTimeOffset? lastChange = null, long? cycle = null) =>
        new(target.Location, phase, _time.GetUtcNow() + Discovery, lastChange, misses, cycle);

    private static async Task<PollStatesResult> QueryAsync(IActorRef manager, CancellationToken ct) =>
        await manager.Ask<PollStatesResult>(new QueryPollStates(), TestTimeouts.RealTimeWaitMax, ct);

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Every_resolved_model_of_every_location_is_started_once()
    {
        // StartedManagerAsync asserts the exact StartPolling sequence: global models first (the duplicate
        // spelling collapsed), then the location's own models.
        await StartedManagerAsync(TestContext.Current.CancellationToken);

        await _region.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task The_query_before_any_report_lists_every_target_in_its_initial_state()
    {
        var ct = TestContext.Current.CancellationToken;
        var manager = await StartedManagerAsync(ct);

        var states = await QueryAsync(manager, ct);

        Assert.Equal(Targets.Length, states.Entries.Count);
        Assert.All(states.Entries, e => Assert.Multiple(
            () => Assert.Equal(PollPhase.Discovery, e.Phase),
            () => Assert.Equal(_time.GetUtcNow(), e.NextPollUtc),
            () => Assert.Null(e.LastChangeUtc),
            () => Assert.Null(e.CycleSeconds),
            () => Assert.Equal(0, e.MissCount)));
        Assert.Equal(Targets.Select(t => t.Location), states.Entries.Select(e => e.Location));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task The_query_returns_the_latest_reported_state_of_each_target()
    {
        var ct = TestContext.Current.CancellationToken;
        var manager = await StartedManagerAsync(ct);
        var change = _time.GetUtcNow();
        var steady = Entry(Targets[0], PollPhase.Steady, lastChange: change, cycle: 10800);
        var missed = Entry(Targets[1], misses: 1, lastChange: change);

        manager.Tell(new PollWorkerReport(Targets[0], PollWorkerOutcome.Changed, steady));
        manager.Tell(new PollWorkerReport(Targets[1], PollWorkerOutcome.Changed, missed with { MissCount = 0 }));
        manager.Tell(new PollWorkerReport(Targets[1], PollWorkerOutcome.Unchanged, missed));

        var states = await QueryAsync(manager, ct);
        Assert.Multiple(
            () => Assert.Equal(steady, states.Entries[0]),
            () => Assert.Equal(missed, states.Entries[1]));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_report_for_an_unknown_target_leaves_the_poll_states_unchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        var manager = await StartedManagerAsync(ct);
        var atlantis = new PollTarget(new Location("atlantis", 0, 0));

        manager.Tell(new PollWorkerReport(atlantis, PollWorkerOutcome.Changed, Entry(atlantis, lastChange: _time.GetUtcNow())));

        var states = await QueryAsync(manager, ct);
        Assert.Multiple(
            () => Assert.Equal(Targets.Length, states.Entries.Count),
            () => Assert.DoesNotContain(states.Entries, e => e.Location.Name == "atlantis"),
            () => Assert.All(states.Entries, e => Assert.Null(e.LastChangeUtc)));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_query_sends_nothing_to_the_region()
    {
        var ct = TestContext.Current.CancellationToken;
        var manager = await StartedManagerAsync(ct);

        await QueryAsync(manager, ct);

        await _region.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Trigger_immediate_poll_for_one_location_reaches_its_worker()
    {
        var ct = TestContext.Current.CancellationToken;
        var manager = await StartedManagerAsync(ct);

        var result = await manager.Ask<TriggerImmediatePollCompleted>(
            new TriggerImmediatePoll(new Location("Lucerne", 0, 0)), TestTimeouts.RealTimeWaitMax, ct);

        var trigger = await _region.ExpectMsgAsync<TriggerLocationPoll>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        Assert.Multiple(
            () => Assert.Equal(1, result.Count),
            () => Assert.Equal(["lucerne"], result.Targets),
            () => Assert.Equal("lucerne", trigger.LocationName));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Trigger_immediate_poll_for_all_returns_all_targets()
    {
        var ct = TestContext.Current.CancellationToken;
        var manager = await StartedManagerAsync(ct);

        var result = await manager.Ask<TriggerImmediatePollCompleted>(
            new TriggerImmediatePoll(null), TestTimeouts.RealTimeWaitMax, ct);

        Assert.Multiple(
            () => Assert.Equal(Targets.Length, result.Count),
            () => Assert.Equal(Targets.Select(t => t.ToString()), result.Targets));
        foreach (var target in Targets)
        {
            var trigger = await _region.ExpectMsgAsync<TriggerLocationPoll>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
            Assert.Equal(target.Id, trigger.LocationName);
        }
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Trigger_immediate_poll_for_unknown_location_returns_zero()
    {
        var ct = TestContext.Current.CancellationToken;
        var manager = await StartedManagerAsync(ct);

        var result = await manager.Ask<TriggerImmediatePollCompleted>(
            new TriggerImmediatePoll(new Location("nonexistent", 0, 0)), TestTimeouts.RealTimeWaitMax, ct);

        Assert.Multiple(
            () => Assert.Equal(0, result.Count),
            () => Assert.Empty(result.Targets));
        await _region.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_running_worker_of_a_removed_target_is_stopped()
    {
        var ct = TestContext.Current.CancellationToken;
        await StartedManagerAsync(ct, "atlantis");

        var stop = await _region.ExpectMsgAsync<StopPolling>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);

        Assert.Equal("atlantis", stop.LocationName);
        await _region.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_terminated_worker_is_started_again()
    {
        var ct = TestContext.Current.CancellationToken;
        var manager = await StartedManagerAsync(ct);
        var worker = CreateTestProbe("worker");
        manager.Tell(new PollWorkerReport(Targets[1], PollWorkerOutcome.Planned, Entry(Targets[1])), worker.Ref);
        await QueryAsync(manager, ct);

        await worker.Ref.GracefulStop(TestTimeouts.RealTimeWaitMax);

        var start = await _region.ExpectMsgAsync<StartPolling>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        Assert.Equal(new StartPolling(Targets[1], Discovery), start);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_successful_poll_updates_the_poll_health_and_a_failure_does_not()
    {
        var ct = TestContext.Current.CancellationToken;
        var manager = await StartedManagerAsync(ct);

        manager.Tell(new PollWorkerReport(Targets[0], PollWorkerOutcome.Failed, Entry(Targets[0])));
        await QueryAsync(manager, ct);
        var afterFailure = await manager.Ask<PollHealthResult>(new QueryPollHealth(), TimeSpan.FromSeconds(3), ct);

        _time.Advance(TimeSpan.FromMinutes(1));
        manager.Tell(new PollWorkerReport(Targets[0], PollWorkerOutcome.Unchanged, Entry(Targets[0])));
        await QueryAsync(manager, ct);

        var afterSuccess = await manager.Ask<PollHealthResult>(new QueryPollHealth(), TimeSpan.FromSeconds(3), ct);
        Assert.Multiple(
            () => Assert.Null(afterFailure.LastSuccessfulPollUtc),
            () => Assert.Equal(_time.GetUtcNow(), afterSuccess.LastSuccessfulPollUtc));
    }
}
