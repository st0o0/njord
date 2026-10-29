using Akka.Actor;
using Akka.Cluster.Sharding;
using Akka.Hosting;
using Akka.Persistence;
using Akka.TestKit;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Messages.Fetch;
using Njord.Messages.Polling;
using Njord.Tests.Shared;

namespace Njord.Polling.Tests;

public sealed class PollWorkerLifecycleTests : Akka.Hosting.TestKit.TestKit
{
    private const string TargetId = "lucerne";
    private static readonly TimeSpan Discovery = TimeSpan.FromMinutes(20);
    private static readonly Location Lucerne = new("lucerne", 47.05, 8.31);
    private static readonly PollTarget Target = new(Lucerne);
    private static readonly IReadOnlyList<DomainKind> EnabledDomains = [DomainKind.Forecast, DomainKind.AirQuality];

    private readonly FakeTimeProvider _time = new(TestTimeConstants.PipelineEpoch);
    private TestProbe _gateway = null!;
    private TestProbe _manager = null!;

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.AddTestPersistence();
        builder.WithActors((_, registry) =>
        {
            _gateway = CreateTestProbe("gateway");
            _manager = CreateTestProbe("manager");
            registry.Register<IForecastGateway>(_gateway.Ref);
            registry.Register<IPollManager>(_manager.Ref);
        });
    }

    private IActorRef CreateWorker(string entityId = TargetId) =>
        Sys.ActorOf(Props.Create(() => new PollWorker(
            entityId, _time, Options.Create(new PollingOptions()),
            Options.Create(TestRequirements.For(EnabledDomains)))), $"worker-{Guid.NewGuid():N}");

    private IActorRef CreateShardedWorker(string entityId = TargetId) =>
        ChildActorOf(Props.Create(() => new PollWorker(
            entityId, _time, Options.Create(new PollingOptions()),
            Options.Create(TestRequirements.For(EnabledDomains)))), $"worker-{Guid.NewGuid():N}");

    private async Task<IActorRef> StartedWorkerAsync(CancellationToken ct)
    {
        var worker = CreateWorker();
        worker.Tell(new StartPolling(Target, Discovery));
        await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        return worker;
    }

    private async Task<PollWorkerReport> ExpectReportAsync(PollWorkerOutcome outcome, CancellationToken ct)
    {
        var report = await _manager.ExpectMsgAsync<PollWorkerReport>(TimeSpan.FromSeconds(5), cancellationToken: ct);
        Assert.Equal(outcome, report.Outcome);
        return report;
    }

    private async Task<FetchDomain> ExpectRequestAsync(CancellationToken ct) =>
        await _gateway.ExpectMsgAsync<FetchDomain>(TimeSpan.FromSeconds(5), cancellationToken: ct);

    private async Task AnswerAllAsync(FetchDomainResponse reply, CancellationToken ct)
    {
        foreach (var _ in EnabledDomains)
        {
            var request = await ExpectRequestAsync(ct);
            _gateway.LastSender.Tell(ForDomain(reply, request.Domain));
        }

        await DrainReportsAsync(EnabledDomains.Count, ct);
    }

    private async Task<PollWorkerReport> AnswerAllAndLastReportAsync(FetchDomainResponse reply, CancellationToken ct)
    {
        foreach (var _ in EnabledDomains)
        {
            var request = await ExpectRequestAsync(ct);
            _gateway.LastSender.Tell(ForDomain(reply, request.Domain));
        }

        return await DrainReportsAsync(EnabledDomains.Count, ct);
    }

    private async Task<PollWorkerReport> DrainReportsAsync(int count, CancellationToken ct)
    {
        PollWorkerReport? last = null;
        for (var i = 0; i < count; i++)
            last = await _manager.ExpectMsgAsync<PollWorkerReport>(TimeSpan.FromSeconds(5), cancellationToken: ct);
        return last!;
    }

    private static FetchDomainResponse ForDomain(FetchDomainResponse reply, DomainKind domain) => reply switch
    {
        FetchDomainCompleted completed => completed with { Domain = domain },
        FetchDomainFailed failed => failed with { Domain = domain },
        FetchDomainRejected rejected => rejected with { Domain = domain },
        _ => reply,
    };

    private static FetchDomainCompleted Completed(int hash) => new(Lucerne, DomainKind.Forecast, hash);

    private async Task StopAsync(IActorRef worker, CancellationToken ct)
    {
        await WatchAsync(worker);
        await worker.GracefulStop(TimeSpan.FromSeconds(15));
        await ExpectTerminatedAsync(worker, cancellationToken: ct);
    }

    private static (string, double, double) Coordinates(Location l) => (l.Name, l.Latitude, l.Longitude);

    // --- Start / coordinates ---

    [Fact(Timeout = 30_000)]
    public async Task A_started_worker_targets_the_coordinates_of_its_location()
    {
        var ct = TestContext.Current.CancellationToken;
        CreateWorker().Tell(new StartPolling(Target, Discovery));

        var report = await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        var request = await ExpectRequestAsync(ct);

        Assert.Multiple(
            () => Assert.Equal(Coordinates(Lucerne), Coordinates(report.Target.Location)),
            () => Assert.Equal(Coordinates(Lucerne), Coordinates(request.Location)));
    }

    [Fact(Timeout = 30_000)]
    public async Task A_restart_with_changed_coordinates_retargets_and_keeps_the_poll_state()
    {
        var ct = TestContext.Current.CancellationToken;
        var moved = new Location("lucerne", 47.10, 8.40);
        var worker = await StartedWorkerAsync(ct);
        var before = await AnswerAllAndLastReportAsync(Completed(1), ct);

        worker.Tell(new StartPolling(new PollTarget(moved), Discovery));

        var after = await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        Assert.Multiple(
            () => Assert.Equal(Coordinates(moved), Coordinates(after.Target.Location)),
            () => Assert.Equal(before.State.LastChangeUtc, after.State.LastChangeUtc),
            () => Assert.Equal(before.State.Phase, after.State.Phase));

        _time.Advance(TimeSpan.FromHours(1));
        worker.Tell(new TriggerLocationPoll(TargetId));
        var request = await ExpectRequestAsync(ct);
        Assert.Equal(Coordinates(moved), Coordinates(request.Location));
    }

    [Fact(Timeout = 30_000)]
    public async Task A_restart_with_identical_target_and_interval_only_reports_the_plan()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAllAsync(Completed(1), ct);

        worker.Tell(new StartPolling(Target, Discovery));

        var report = await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        Assert.Equal(Coordinates(Lucerne), Coordinates(report.Target.Location));
        await _gateway.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), ct);
    }

    // --- Recovery ---

    [Fact(Timeout = 30_000)]
    public async Task A_recovered_worker_keeps_the_coordinates_of_its_location()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAllAsync(Completed(1), ct);
        await StopAsync(worker, ct);

        CreateWorker();

        var report = await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        Assert.Equal(Coordinates(Lucerne), Coordinates(report.Target.Location));
    }

    [Fact(Timeout = 30_000)]
    public async Task A_restart_with_changed_coordinates_survives_a_recovery()
    {
        var ct = TestContext.Current.CancellationToken;
        var moved = new Location("lucerne", 47.10, 8.40);
        var worker = await StartedWorkerAsync(ct);
        worker.Tell(new StartPolling(new PollTarget(moved), Discovery));
        await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        await StopAsync(worker, ct);

        CreateWorker();

        var report = await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        Assert.Equal(Coordinates(moved), Coordinates(report.Target.Location));
    }

    [Fact(Timeout = 30_000)]
    public async Task A_restarted_worker_keeps_its_phase_cycle_and_next_poll()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);

        await AnswerAllAsync(Completed(1), ct);

        _time.Advance(TimeSpan.FromHours(1));
        worker.Tell(new TriggerLocationPoll(TargetId));
        await AnswerAllAsync(Completed(2), ct);

        _time.Advance(TimeSpan.FromHours(1));
        worker.Tell(new TriggerLocationPoll(TargetId));
        var before = await AnswerAllAndLastReportAsync(Completed(3), ct);
        Assert.Equal(PollPhase.Steady, before.State.Phase);

        await StopAsync(worker, ct);
        CreateWorker();

        var after = await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        Assert.Multiple(
            () => Assert.Equal(PollPhase.Steady, after.State.Phase),
            () => Assert.Equal(before.State.CycleSeconds, after.State.CycleSeconds),
            () => Assert.Equal(before.State.NextPollUtc, after.State.NextPollUtc),
            () => Assert.Equal(before.State.LastChangeUtc, after.State.LastChangeUtc));
        await _gateway.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(500), ct);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_recovered_next_poll_in_the_past_polls_immediately()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAllAsync(Completed(1), ct);
        await StopAsync(worker, ct);

        _time.Advance(TimeSpan.FromHours(1));
        CreateWorker();

        await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        var request = await ExpectRequestAsync(ct);
        Assert.Equal(DomainKind.Forecast, request.Domain);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_snapshot_is_saved_after_fifty_events_and_recovery_uses_it()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);

        for (var i = 0; i < 25; i++)
        {
            if (i > 0)
            {
                _time.Advance(TimeSpan.FromHours(1));
                worker.Tell(new TriggerLocationPoll(TargetId));
            }

            await AnswerAllAsync(Completed(1000 + i), ct);
        }

        var store = Akka.Persistence.Persistence.Instance.Apply(Sys).SnapshotStoreFor(null);
        await AwaitAssertAsync(async () =>
        {
            var loaded = await store.Ask<LoadSnapshotResult>(
                new LoadSnapshot($"poll-worker-{TargetId}", SnapshotSelectionCriteria.Latest, long.MaxValue),
                TimeSpan.FromSeconds(15), ct);
            Assert.NotNull(loaded.Snapshot);
            Assert.True(loaded.Snapshot.Metadata.SequenceNr >= 50);
        }, TimeSpan.FromSeconds(5), cancellationToken: ct);

        await StopAsync(worker, ct);
        CreateWorker();

        var recovered = await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        Assert.Multiple(
            () => Assert.Equal(PollPhase.Steady, recovered.State.Phase),
            () => Assert.Equal(3600L, recovered.State.CycleSeconds),
            () => Assert.Equal(_time.GetUtcNow(), recovered.State.LastChangeUtc));
    }

    // --- Stop ---

    [Fact(Timeout = 30_000)]
    public async Task A_stopped_worker_stops_polling_and_keeps_its_journal()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = CreateShardedWorker();
        worker.Tell(new StartPolling(Target, Discovery));
        await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        await AnswerAllAsync(Completed(42), ct);

        await WatchAsync(worker);
        worker.Tell(new StopPolling(TargetId));
        var passivate = await ExpectMsgAsync<Passivate>(TimeSpan.FromSeconds(5), cancellationToken: ct);
        worker.Tell(passivate.StopMessage);
        await ExpectTerminatedAsync(worker, cancellationToken: ct);

        _time.Advance(TimeSpan.FromHours(1));
        var recovered = CreateWorker();
        await _manager.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(500), ct);
        await _gateway.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(100), ct);

        recovered.Tell(new StartPolling(Target, Discovery));
        var report = await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        Assert.Equal(_time.GetUtcNow() - TimeSpan.FromHours(1), report.State.LastChangeUtc);
    }

    // --- Gateway connection ---

    [Fact(Timeout = 30_000)]
    public async Task Real_poll_worker_and_gateway_connect_and_poll()
    {
        var ct = TestContext.Current.CancellationToken;
        var fetchCalled = new TaskCompletionSource<string>();

        var fakeGateway = Sys.ActorOf(
            Props.Create(() => new FakeGatewayActor(fetchCalled)), "gateway");
        ActorRegistry.Register<IForecastGateway>(fakeGateway, overwrite: true);

        var target = new PollTarget(new Location("test", 47.0, 8.3));
        var requirements = Options.Create(TestRequirements.For(DomainKind.Forecast));
        var worker = Sys.ActorOf(
            Props.Create(() => new PollWorker(target.Id, _time, Options.Create(new PollingOptions()), requirements)),
            "real-worker");
        worker.Tell(new StartPolling(target, TimeSpan.FromMinutes(20)));

        var location = await fetchCalled.Task.WaitAsync(TimeSpan.FromSeconds(8), ct);
        Assert.Equal("test", location);
    }

    private sealed class FakeGatewayActor : ReceiveActor
    {
        public FakeGatewayActor(TaskCompletionSource<string> fetchCalled)
        {
            Receive<FetchDomain>(msg =>
            {
                fetchCalled.TrySetResult(msg.Location.Name);
                Sender.Tell(new FetchDomainCompleted(msg.Location, msg.Domain, msg.Location.GetHashCode() ^ msg.Domain.GetHashCode()));
            });
        }
    }
}
