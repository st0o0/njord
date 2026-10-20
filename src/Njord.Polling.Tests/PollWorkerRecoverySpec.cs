using Akka.Actor;
using Akka.Persistence;
using Njord.Forecast;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Polling.Tests;

public sealed class PollWorkerRecoverySpec : PollWorkerSpecBase
{
    [Fact(Timeout = TestTimeouts.Hosted)]
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

    [Fact(Timeout = TestTimeouts.Hosted)]
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
        Assert.Equal(DomainKind.Weather, request.Domain);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
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
                TestTimeouts.RealTimeWaitMax, ct);
            Assert.NotNull(loaded.Snapshot);
            Assert.True(loaded.Snapshot.Metadata.SequenceNr >= 50);
        }, TestTimeouts.AwaitAssertMax, cancellationToken: ct);

        await StopAsync(worker, ct);
        CreateWorker();

        var recovered = await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        Assert.Multiple(
            () => Assert.Equal(PollPhase.Steady, recovered.State.Phase),
            () => Assert.Equal(3600L, recovered.State.CycleSeconds),
            () => Assert.Equal(_time.GetUtcNow(), recovered.State.LastChangeUtc));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_stopped_worker_stops_polling_and_keeps_its_journal()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAllAsync(Completed(42), ct);

        await WatchAsync(worker);
        worker.Tell(new StopPolling(TargetId));
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
