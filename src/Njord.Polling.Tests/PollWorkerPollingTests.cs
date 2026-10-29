using Akka.Actor;
using Akka.Hosting;
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

public sealed class PollWorkerPollingTests : Akka.Hosting.TestKit.TestKit
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

    private IActorRef CreateWorker(IReadOnlyList<DomainKind>? domains = null, string entityId = TargetId) =>
        Sys.ActorOf(Props.Create(() => new PollWorker(
            entityId, _time, Options.Create(new PollingOptions()),
            Options.Create(TestRequirements.For(domains ?? EnabledDomains)))), $"worker-{Guid.NewGuid():N}");

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

    // --- Outcomes ---

    [Fact(Timeout = 30_000)]
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
            () => Assert.Equal(DomainKind.Forecast, request.Domain));
    }

    [Fact(Timeout = 30_000)]
    public async Task A_changed_hash_is_reported_with_the_next_discovery_poll()
    {
        var ct = TestContext.Current.CancellationToken;
        await StartedWorkerAsync(ct);

        var report = await AnswerAllAndLastReportAsync(Completed(42), ct);

        Assert.Multiple(
            () => Assert.Equal(_time.GetUtcNow(), report.State.LastChangeUtc),
            () => Assert.Equal(_time.GetUtcNow() + Discovery, report.State.NextPollUtc),
            () => Assert.Equal(0, report.State.MissCount));
    }

    [Fact(Timeout = 30_000)]
    public async Task An_unchanged_hash_is_reported_as_a_miss()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAllAsync(Completed(42), ct);

        worker.Tell(new TriggerLocationPoll(TargetId));
        var report = await AnswerAllAndLastReportAsync(Completed(42), ct);

        Assert.Multiple(
            () => Assert.Equal(1, report.State.MissCount),
            () => Assert.NotNull(report.State.LastChangeUtc));
    }

    [Fact(Timeout = 30_000)]
    public async Task Three_consistent_changes_learn_the_cycle()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);

        await AnswerAllAsync(Completed(1), ct);

        _time.Advance(TimeSpan.FromHours(3));
        worker.Tell(new TriggerLocationPoll(TargetId));
        await AnswerAllAsync(Completed(2), ct);

        _time.Advance(TimeSpan.FromHours(3));
        worker.Tell(new TriggerLocationPoll(TargetId));
        var reports = new List<PollWorkerReport>();
        foreach (var _ in EnabledDomains)
        {
            var request = await ExpectRequestAsync(ct);
            _gateway.LastSender.Tell(ForDomain(Completed(3), request.Domain));
        }

        for (var i = 0; i < EnabledDomains.Count; i++)
            reports.Add(await _manager.ExpectMsgAsync<PollWorkerReport>(TimeSpan.FromSeconds(5), cancellationToken: ct));

        var third = reports[^1];
        Assert.Multiple(
            () => Assert.Equal(PollPhase.Steady, third.State.Phase),
            () => Assert.Equal(10800L, third.State.CycleSeconds));
    }

    [Fact(Timeout = 30_000)]
    public async Task A_reply_spelled_differently_updates_the_configured_target()
    {
        var ct = TestContext.Current.CancellationToken;
        await StartedWorkerAsync(ct);

        var report = await AnswerAllAndLastReportAsync(
            new FetchDomainCompleted(new Location("LUCERNE", 0, 0), DomainKind.Forecast, 42), ct);

        Assert.Multiple(
            () => Assert.Equal(Target, report.Target),
            () => Assert.Equal(Lucerne, report.State.Location),
            () => Assert.NotNull(report.State.LastChangeUtc));
    }

    // --- Triggers ---

    [Fact(Timeout = 30_000)]
    public async Task A_poll_asks_only_the_configured_domains()
    {
        var ct = TestContext.Current.CancellationToken;
        IReadOnlyList<DomainKind> domains = [DomainKind.Forecast, DomainKind.Flood];
        var worker = CreateWorker(domains);

        worker.Tell(new StartPolling(Target, Discovery));

        var first = await ExpectRequestAsync(ct);
        var second = await ExpectRequestAsync(ct);
        await _gateway.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), ct);
        Assert.Equal([DomainKind.Forecast, DomainKind.Flood], new[] { first.Domain, second.Domain }.Order());
    }

    [Fact(Timeout = 30_000)]
    public async Task A_trigger_during_a_request_in_flight_sends_no_second_request()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        var inFlight = new List<(IActorRef Sender, DomainKind Domain)>();
        foreach (var _ in EnabledDomains)
        {
            var request = await ExpectRequestAsync(ct);
            inFlight.Add((_gateway.LastSender, request.Domain));
        }

        worker.Tell(new TriggerLocationPoll(TargetId));
        await _gateway.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), ct);

        foreach (var (sender, domain) in inFlight)
            sender.Tell(ForDomain(Completed(42), domain));

        await DrainReportsAsync(EnabledDomains.Count, ct);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_trigger_after_the_outcome_reaches_the_gateway()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAllAsync(Completed(42), ct);

        worker.Tell(new TriggerLocationPoll(TargetId));

        var request = await ExpectRequestAsync(ct);
        Assert.Equal(Target, new PollTarget(request.Location));
    }

    [Fact(Timeout = 30_000)]
    public async Task A_replaced_gateway_is_used_by_the_next_poll()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAllAsync(Completed(42), ct);

        var replacement = CreateTestProbe("gateway-replacement");
        ActorRegistry.Register<IForecastGateway>(replacement.Ref, overwrite: true);
        worker.Tell(new TriggerLocationPoll(TargetId));

        var request = await replacement.ExpectMsgAsync<FetchDomain>(TimeSpan.FromSeconds(5), cancellationToken: ct);
        Assert.Equal(DomainKind.Forecast, request.Domain);
        await _gateway.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(200), ct);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_worker_that_was_never_started_ignores_triggers()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = CreateWorker();

        worker.Tell(new TriggerLocationPoll(TargetId));

        await _gateway.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), ct);
        await _manager.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(100), ct);
    }

    // --- Intervals ---

    [Fact(Timeout = 30_000)]
    public async Task A_shorter_discovery_interval_replans_the_pending_poll()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAllAsync(Completed(42), ct);

        worker.Tell(new StartPolling(Target, TimeSpan.FromMilliseconds(100)));

        await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        var next = await ExpectRequestAsync(ct);
        Assert.Equal(DomainKind.Forecast, next.Domain);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_longer_discovery_interval_moves_the_next_poll_out()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAllAsync(Completed(42), ct);

        worker.Tell(new StartPolling(Target, TimeSpan.FromHours(2)));

        var report = await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        Assert.Equal(_time.GetUtcNow() + TimeSpan.FromHours(2), report.State.NextPollUtc);
    }

    [Fact(Timeout = 30_000)]
    public async Task Without_a_change_the_next_discovery_poll_waits_for_the_configured_interval()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAllAsync(Completed(42), ct);

        worker.Tell(new StartPolling(Target, Discovery));

        var report = await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        Assert.Equal(_time.GetUtcNow() + Discovery, report.State.NextPollUtc);
        await _gateway.ExpectNoMsgAsync(TimeSpan.FromSeconds(1), ct);
    }

    // --- Domain scheduling ---

    [Fact(Timeout = 30_000)]
    public async Task A_restarted_worker_polls_only_the_domains_that_are_due()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);

        foreach (var _ in EnabledDomains)
        {
            var request = await ExpectRequestAsync(ct);
            _gateway.LastSender.Tell(request.Domain == DomainKind.Forecast
                ? new FetchDomainCompleted(Lucerne, DomainKind.Forecast, 1)
                : new FetchDomainFailed(Lucerne, request.Domain, PollFailureReason.Transport, "down"));
        }

        await DrainReportsAsync(EnabledDomains.Count, ct);
        await StopAsync(worker, ct);

        _time.Advance(TimeSpan.FromMinutes(2));
        CreateWorker();

        await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        var next = await ExpectRequestAsync(ct);
        Assert.Equal(DomainKind.AirQuality, next.Domain);
        await _gateway.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(500), ct);
    }

    [Fact(Timeout = 30_000)]
    public async Task Learned_domain_cycles_survive_a_restart()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);

        await AnswerAllAsync(Completed(0), ct);
        for (var hash = 1; hash <= 2; hash++)
        {
            _time.Advance(TimeSpan.FromHours(1));
            worker.Tell(new TriggerLocationPoll(TargetId));
            await AnswerAllAsync(Completed(hash), ct);
        }

        var expectedNext = _time.GetUtcNow() + TimeSpan.FromHours(1) - DomainPollStateExtensions.SteadyBuffer;
        await StopAsync(worker, ct);
        CreateWorker();

        var recovered = await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        Assert.Equal(expectedNext, recovered.State.NextPollUtc);
        await _gateway.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(500), ct);
    }

}
