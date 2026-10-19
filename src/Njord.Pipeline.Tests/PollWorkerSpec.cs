using Akka.Actor;
using Akka.DependencyInjection;
using Akka.Hosting;
using Akka.Persistence;
using Akka.TestKit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Njord.Core.Actors;
using Njord.Core.Configuration.Polling;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Tests.Shared;

namespace Njord.Pipeline.Tests;

// The worker is created directly from the resolver; a probe plays the gateway (it answers through LastSender)
// and another probe plays the poll manager.
public sealed class PollWorkerSpec : NjordActorSpec
{
    private const string TargetId = "lucerne";
    private static readonly TimeSpan Discovery = TimeSpan.FromMinutes(20);
    private static readonly Location Lucerne = new("lucerne", 0, 0);
    private static readonly PollTarget Target = new(Lucerne);

    private readonly FakeTimeProvider _time = TestTimeouts.AtEpoch();
    private TestProbe _gateway = null!;
    private TestProbe _manager = null!;

    protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        services.AddSingleton<TimeProvider>(_time);
        services.AddSingleton(FetchTimeouts.Default);
    }

    protected override void ConfigureActors(AkkaConfigurationBuilder builder, IServiceProvider provider) =>
        builder.WithActors((_, registry) =>
        {
            _gateway = CreateTestProbe("gateway");
            _manager = CreateTestProbe("manager");
            registry.Register<IForecastGateway>(_gateway.Ref);
            registry.Register<IPollManager>(_manager.Ref);
        });

    private IActorRef CreateWorker(string entityId = TargetId) =>
        Sys.ActorOf(DependencyResolver.For(Sys).Props<PollWorker>(entityId), $"worker-{Guid.NewGuid():N}");

    private async Task<IActorRef> StartedWorkerAsync(CancellationToken ct)
    {
        var worker = CreateWorker();
        worker.Tell(new StartPolling(Target, Discovery));
        await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        return worker;
    }

    private async Task<FetchDomain> ExpectRequestAsync(CancellationToken ct) =>
        await _gateway.ExpectMsgAsync<FetchDomain>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);

    // A poll round asks every enabled domain; each one is answered with the same reply, retargeted to its domain.
    private async Task AnswerAsync(FetchDomainResponse reply, CancellationToken ct)
    {
        foreach (var _ in PollWorker.EnabledDomains)
        {
            var request = await ExpectRequestAsync(ct);
            _gateway.LastSender.Tell(ForDomain(reply, request.Domain));
        }
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
        var report = await _manager.ExpectMsgAsync<PollWorkerReport>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        Assert.Equal(outcome, report.Outcome);
        return report;
    }

    private static FetchDomainCompleted Completed(int hash) => new(Lucerne, DomainKind.Weather, hash);

    private async Task StopAsync(IActorRef worker, CancellationToken ct)
    {
        await WatchAsync(worker);
        await worker.GracefulStop(TestTimeouts.RealTimeWaitMax);
        await ExpectTerminatedAsync(worker, cancellationToken: ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_first_start_reports_the_initial_state_and_polls_immediately()
    {
        var ct = TestContext.Current.CancellationToken;
        CreateWorker().Tell(new StartPolling(Target, Discovery));

        var report = await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        var request = await ExpectRequestAsync(ct);

        Assert.Multiple(
            () => Assert.Equal(Target, report.Target),
            () => Assert.Equal(PollPhase.Discovery, report.State.Phase),
            () => Assert.Equal(_time.GetUtcNow(), report.State.NextPollUtc),
            () => Assert.Null(report.State.LastChangeUtc),
            () => Assert.Equal(Lucerne, request.Location),
            () => Assert.Equal(DomainKind.Weather, request.Domain));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_changed_hash_is_reported_with_the_next_discovery_poll()
    {
        var ct = TestContext.Current.CancellationToken;
        await StartedWorkerAsync(ct);

        await AnswerAsync(Completed(42), ct);

        var report = await ExpectReportAsync(PollWorkerOutcome.Changed, ct);
        Assert.Multiple(
            () => Assert.Equal(_time.GetUtcNow(), report.State.LastChangeUtc),
            () => Assert.Equal(_time.GetUtcNow() + Discovery, report.State.NextPollUtc),
            () => Assert.Equal(0, report.State.MissCount));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task An_unchanged_hash_is_reported_as_a_miss()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAsync(Completed(42), ct);
        await ExpectReportAsync(PollWorkerOutcome.Changed, ct);

        worker.Tell(new TriggerLocationPoll(TargetId));
        await AnswerAsync(Completed(42), ct);

        var report = await ExpectReportAsync(PollWorkerOutcome.Unchanged, ct);
        Assert.Multiple(
            () => Assert.Equal(1, report.State.MissCount),
            () => Assert.NotNull(report.State.LastChangeUtc));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_second_change_learns_the_cycle()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAsync(Completed(1), ct);
        await ExpectReportAsync(PollWorkerOutcome.Changed, ct);

        _time.Advance(TimeSpan.FromHours(3));
        worker.Tell(new TriggerLocationPoll(TargetId));
        await AnswerAsync(Completed(2), ct);

        var report = await ExpectReportAsync(PollWorkerOutcome.Changed, ct);
        Assert.Multiple(
            () => Assert.Equal(PollPhase.Steady, report.State.Phase),
            () => Assert.Equal(10800L, report.State.CycleSeconds),
            () => Assert.Equal(_time.GetUtcNow() + TimeSpan.FromHours(3) + TimeSpan.FromMinutes(1), report.State.NextPollUtc));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_reply_spelled_differently_updates_the_configured_target()
    {
        var ct = TestContext.Current.CancellationToken;
        await StartedWorkerAsync(ct);

        await AnswerAsync(new FetchDomainCompleted(new Location("LUCERNE", 0, 0), DomainKind.Weather, 42), ct);

        var report = await ExpectReportAsync(PollWorkerOutcome.Changed, ct);
        Assert.Multiple(
            () => Assert.Equal(Target, report.Target),
            () => Assert.Equal(Lucerne, report.State.Location),
            () => Assert.NotNull(report.State.LastChangeUtc));
    }

    [Theory(Timeout = TestTimeouts.Hosted)]
    [InlineData(PollFailureReason.Transport, 60)]
    [InlineData(PollFailureReason.ModelUnavailable, 60)]
    [InlineData(PollFailureReason.MalformedPayload, 60)]
    [InlineData(PollFailureReason.RateLimited, 300)]
    public async Task A_failure_is_reported_with_its_backoff(PollFailureReason reason, int delaySeconds)
    {
        var ct = TestContext.Current.CancellationToken;
        await StartedWorkerAsync(ct);

        await AnswerAsync(new FetchDomainFailed(Lucerne, DomainKind.Weather, reason, "simulated"), ct);

        var report = await ExpectReportAsync(PollWorkerOutcome.Failed, ct);
        Assert.Equal(_time.GetUtcNow() + TimeSpan.FromSeconds(delaySeconds), report.State.NextPollUtc);
    }

    [Theory(Timeout = TestTimeouts.Hosted)]
    [InlineData(FetchRejectionReason.Budget, 300)]
    [InlineData(FetchRejectionReason.QueueFull, 60)]
    [InlineData(FetchRejectionReason.Expired, 60)]
    [InlineData(FetchRejectionReason.PipelineUnavailable, 5)]
    public async Task A_rejection_is_reported_as_failed_with_its_backoff(FetchRejectionReason reason, int delaySeconds)
    {
        var ct = TestContext.Current.CancellationToken;
        await StartedWorkerAsync(ct);

        await AnswerAsync(new FetchDomainRejected(Lucerne, DomainKind.Weather, reason), ct);

        var report = await ExpectReportAsync(PollWorkerOutcome.Failed, ct);
        Assert.Equal(_time.GetUtcNow() + TimeSpan.FromSeconds(delaySeconds), report.State.NextPollUtc);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_pipeline_unavailable_rejection_retries_soon_without_growing_the_backoff()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAsync(new FetchDomainFailed(Lucerne, DomainKind.Weather, PollFailureReason.Transport, "simulated"), ct);
        await ExpectReportAsync(PollWorkerOutcome.Failed, ct);

        worker.Tell(new TriggerLocationPoll(TargetId));
        await AnswerAsync(new FetchDomainRejected(Lucerne, DomainKind.Weather, FetchRejectionReason.PipelineUnavailable), ct);
        var deferred = await ExpectReportAsync(PollWorkerOutcome.Failed, ct);

        // The next transient failure continues the backoff from one failure, not from two.
        worker.Tell(new TriggerLocationPoll(TargetId));
        await AnswerAsync(new FetchDomainRejected(Lucerne, DomainKind.Weather, FetchRejectionReason.QueueFull), ct);
        var queueFull = await ExpectReportAsync(PollWorkerOutcome.Failed, ct);

        Assert.Multiple(
            () => Assert.Equal(_time.GetUtcNow() + PollWorkerState.PipelineUnavailableRetry, deferred.State.NextPollUtc),
            () => Assert.Equal(_time.GetUtcNow() + TimeSpan.FromMinutes(2), queueFull.State.NextPollUtc));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_trigger_during_a_request_in_flight_sends_no_second_request()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        var inFlight = new List<(IActorRef Sender, DomainKind Domain)>();
        foreach (var _ in PollWorker.EnabledDomains)
        {
            var request = await ExpectRequestAsync(ct);
            inFlight.Add((_gateway.LastSender, request.Domain));
        }

        worker.Tell(new TriggerLocationPoll(TargetId));
        await _gateway.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), ct);

        // The requests in flight still end normally and their outcome is reported.
        foreach (var (sender, domain) in inFlight)
        {
            sender.Tell(ForDomain(Completed(42), domain));
        }

        await ExpectReportAsync(PollWorkerOutcome.Changed, ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_trigger_after_the_outcome_reaches_the_gateway()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAsync(Completed(42), ct);
        await ExpectReportAsync(PollWorkerOutcome.Changed, ct);

        worker.Tell(new TriggerLocationPoll(TargetId));

        var request = await ExpectRequestAsync(ct);
        Assert.Equal(Target, new PollTarget(request.Location));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_replaced_gateway_is_used_by_the_next_poll()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAsync(Completed(42), ct);
        await ExpectReportAsync(PollWorkerOutcome.Changed, ct);

        var replacement = CreateTestProbe("gateway-replacement");
        ActorRegistry.Register<IForecastGateway>(replacement.Ref, overwrite: true);
        worker.Tell(new TriggerLocationPoll(TargetId));

        var request = await replacement.ExpectMsgAsync<FetchDomain>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        Assert.Equal(DomainKind.Weather, request.Domain);
        await _gateway.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(200), ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_shorter_discovery_interval_replans_the_pending_poll()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAsync(Completed(42), ct);
        await ExpectReportAsync(PollWorkerOutcome.Changed, ct);

        worker.Tell(new StartPolling(Target, TimeSpan.FromMilliseconds(100)));

        await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        var next = await ExpectRequestAsync(ct);
        Assert.Equal(DomainKind.Weather, next.Domain);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_longer_discovery_interval_moves_the_next_poll_out()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAsync(Completed(42), ct);
        await ExpectReportAsync(PollWorkerOutcome.Changed, ct);

        worker.Tell(new StartPolling(Target, TimeSpan.FromHours(2)));

        var report = await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        Assert.Equal(_time.GetUtcNow() + TimeSpan.FromHours(2), report.State.NextPollUtc);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Without_a_change_the_next_discovery_poll_waits_for_the_configured_interval()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAsync(Completed(42), ct);
        await ExpectReportAsync(PollWorkerOutcome.Changed, ct);

        worker.Tell(new StartPolling(Target, Discovery));

        var report = await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        Assert.Equal(_time.GetUtcNow() + Discovery, report.State.NextPollUtc);
        await _gateway.ExpectNoMsgAsync(TimeSpan.FromSeconds(1), ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_restarted_worker_keeps_its_phase_cycle_and_next_poll()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAsync(Completed(1), ct);
        await ExpectReportAsync(PollWorkerOutcome.Changed, ct);
        _time.Advance(TimeSpan.FromHours(3));
        worker.Tell(new TriggerLocationPoll(TargetId));
        await AnswerAsync(Completed(2), ct);
        var before = await ExpectReportAsync(PollWorkerOutcome.Changed, ct);

        await StopAsync(worker, ct);
        CreateWorker();

        // Recovered in the started state: it reports on its own and waits for its persisted next poll.
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
        await AnswerAsync(Completed(1), ct);
        await ExpectReportAsync(PollWorkerOutcome.Changed, ct);
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

        // The start is event 1; 49 more outcomes reach sequence number 50.
        for (var i = 0; i < 49; i++)
        {
            if (i > 0)
            {
                _time.Advance(TimeSpan.FromHours(1));
                worker.Tell(new TriggerLocationPoll(TargetId));
            }

            await AnswerAsync(Completed(1000 + i), ct);
            await ExpectReportAsync(PollWorkerOutcome.Changed, ct);
        }

        var store = Akka.Persistence.Persistence.Instance.Apply(Sys).SnapshotStoreFor(null);
        await AwaitAssertAsync(async () =>
        {
            var loaded = await store.Ask<LoadSnapshotResult>(
                new LoadSnapshot($"poll-worker-{TargetId}", SnapshotSelectionCriteria.Latest, long.MaxValue),
                TestTimeouts.RealTimeWaitMax, ct);
            Assert.NotNull(loaded.Snapshot);
            Assert.Equal(50, loaded.Snapshot.Metadata.SequenceNr);
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
        await AnswerAsync(Completed(42), ct);
        await ExpectReportAsync(PollWorkerOutcome.Changed, ct);

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

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_worker_that_was_never_started_ignores_triggers()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = CreateWorker();

        worker.Tell(new TriggerLocationPoll(TargetId));

        await _gateway.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), ct);
        await _manager.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(100), ct);
    }
}
