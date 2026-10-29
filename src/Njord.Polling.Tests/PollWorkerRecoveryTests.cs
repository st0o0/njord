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

// The worker is created with explicit constructor arguments; a probe plays the gateway (it answers through LastSender)
// and another probe plays the poll manager.
public sealed class PollWorkerRecoveryTests : Akka.Hosting.TestKit.TestKit
{
    private const string TargetId = "lucerne";
    private static readonly TimeSpan Discovery = TimeSpan.FromMinutes(20);
    private static readonly Location Lucerne = new("lucerne", 47.05, 8.31);
    private static readonly PollTarget Target = new(Lucerne);
    private static readonly IReadOnlyList<DomainKind> EnabledDomains = [DomainKind.Forecast, DomainKind.AirQuality];

    private static IOptions<DomainRequirements> RequirementsFor(IReadOnlyList<DomainKind> domains) =>
        Options.Create(TestRequirements.For(domains));

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

    private Props WorkerProps(string entityId) =>
        Props.Create(() => new PollWorker(_time, Options.Create(new PollingOptions()), RequirementsFor(EnabledDomains), entityId));

    private IActorRef CreateWorker(string entityId = TargetId) =>
        Sys.ActorOf(WorkerProps(entityId), $"worker-{Guid.NewGuid():N}");

    // A child of the test actor: it plays the shard and receives the worker's Passivate request.
    private IActorRef CreateShardedWorker(string entityId = TargetId) =>
        ChildActorOf(WorkerProps(entityId), $"worker-{Guid.NewGuid():N}");

    private async Task<IActorRef> StartedWorkerAsync(CancellationToken ct)
    {
        var worker = CreateWorker();
        worker.Tell(new StartPolling(Target, Discovery));
        await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        return worker;
    }

    private async Task<FetchDomain> ExpectRequestAsync(CancellationToken ct) =>
        await _gateway.ExpectMsgAsync<FetchDomain>(TimeSpan.FromSeconds(5), cancellationToken: ct);

    // A poll asks every enabled domain independently; each one is answered with the same reply, retargeted to its domain.
    // Each domain produces its own report, so we drain all requests and all reports.
    private async Task AnswerAllAsync(FetchDomainResponse reply, CancellationToken ct)
    {
        foreach (var _ in EnabledDomains)
        {
            var request = await ExpectRequestAsync(ct);
            _gateway.LastSender.Tell(ForDomain(reply, request.Domain));
        }

        await DrainReportsAsync(EnabledDomains.Count, ct);
    }

    // Answer all domain requests and return the last report (for backward-compatible assertions).
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
        {
            last = await _manager.ExpectMsgAsync<PollWorkerReport>(TimeSpan.FromSeconds(5), cancellationToken: ct);
        }

        return last!;
    }

    private static FetchDomainResponse ForDomain(FetchDomainResponse reply, DomainKind domain) => reply switch
    {
        FetchDomainCompleted completed => completed with { Domain = domain },
        FetchDomainFailed failed => failed with { Domain = domain },
        FetchDomainRejected rejected => rejected with { Domain = domain },
        _ => reply,
    };

    private async Task<PollWorkerReport> ExpectReportAsync(PollWorkerOutcome outcome, CancellationToken ct)
    {
        var report = await _manager.ExpectMsgAsync<PollWorkerReport>(TimeSpan.FromSeconds(5), cancellationToken: ct);
        Assert.Equal(outcome, report.Outcome);
        return report;
    }

    private static FetchDomainCompleted Completed(int hash) => new(Lucerne, DomainKind.Forecast, hash);

    private async Task StopAsync(IActorRef worker, CancellationToken ct)
    {
        await WatchAsync(worker);
        await worker.GracefulStop(TimeSpan.FromSeconds(15));
        await ExpectTerminatedAsync(worker, cancellationToken: ct);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_restarted_worker_keeps_its_phase_cycle_and_next_poll()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);

        // Three changes at 1 h intervals to reach Steady (domain needs two consistent intervals).
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

        // The start is event 1; every domain fetch persists 1 DomainPolled event per domain,
        // so with 2 enabled domains each trigger produces 2 events.
        // After event 1 (PollingStarted), we need 49 more DomainPolled events to cross 50.
        // With 2 domains per trigger, that's ~25 triggers to get 50 domain events.
        // 25 triggers * 2 domains = 50 events; total = 1 + 50 = 51 -> snapshot at 50.
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

        // Recovered as stopped: no report, no poll, even when its next poll is due.
        _time.Advance(TimeSpan.FromHours(1));
        var recovered = CreateWorker();
        await _manager.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(500), ct);
        await _gateway.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(100), ct);

        // Started again, it continues from its journal.
        recovered.Tell(new StartPolling(Target, Discovery));
        var report = await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        Assert.Equal(_time.GetUtcNow() - TimeSpan.FromHours(1), report.State.LastChangeUtc);
    }
}
