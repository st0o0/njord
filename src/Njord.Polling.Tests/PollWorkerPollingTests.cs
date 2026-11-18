using Akka.Actor;
using Akka.Hosting;
using Akka.TestKit;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Core.Polling;
using Njord.Messages.Fetch;
using Njord.Messages.Polling;
using Njord.Tests.Shared;

namespace Njord.Polling.Tests;

public sealed class PollWorkerPollingTests : Akka.Hosting.TestKit.TestKit
{
    private const string TargetId = "lucerne";
    private static readonly TimeSpan _forecastInterval = TimeSpan.FromMinutes(60);
    private static readonly Location _lucerne = new("lucerne", 47.05, 8.31);
    private static readonly PollTarget _target = new(_lucerne);
    private static readonly IReadOnlyList<DomainKind> _enabledDomains = [DomainKind.Forecast, DomainKind.AirQuality];

    private static readonly Dictionary<int, TimeSpan> _intervals = new()
    {
        [(int)DomainKind.Forecast] = _forecastInterval,
        [(int)DomainKind.AirQuality] = _forecastInterval,
    };

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
            entityId, _time,
            Options.Create(TestRequirements.For(domains ?? _enabledDomains)))), $"worker-{Guid.NewGuid():N}");

    private async Task<IActorRef> StartedWorkerAsync(CancellationToken ct)
    {
        var worker = CreateWorker();
        worker.Tell(new StartPolling(_target, _intervals));
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
        foreach (var _ in _enabledDomains)
        {
            var request = await ExpectRequestAsync(ct);
            _gateway.LastSender.Tell(ForDomain(reply, request.Domain));
        }

        await DrainReportsAsync(_enabledDomains.Count, ct);
    }

    private async Task<PollWorkerReport> AnswerAllAndLastReportAsync(FetchDomainResponse reply, CancellationToken ct)
    {
        foreach (var _ in _enabledDomains)
        {
            var request = await ExpectRequestAsync(ct);
            _gateway.LastSender.Tell(ForDomain(reply, request.Domain));
        }

        return await DrainReportsAsync(_enabledDomains.Count, ct);
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

    private static FetchDomainCompleted Completed(int hash) => new(_lucerne, DomainKind.Forecast, hash);

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
        CreateWorker().Tell(new StartPolling(_target, _intervals));

        var report = await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        var request = await ExpectRequestAsync(ct);

        Assert.Multiple(
            () => Assert.Equal(_target, report.Target),
            () => Assert.Equal(PollPhase.Active, report.State.Phase),
            () => Assert.Equal(_time.GetUtcNow(), report.State.NextPollUtc),
            () => Assert.Null(report.State.LastChangeUtc),
            () => Assert.Equal(_lucerne, request.Location),
            () => Assert.Equal(DomainKind.Forecast, request.Domain));
    }

    [Fact(Timeout = 30_000)]
    public async Task A_changed_hash_is_reported_with_the_next_poll_at_effective_interval()
    {
        var ct = TestContext.Current.CancellationToken;
        await StartedWorkerAsync(ct);

        var report = await AnswerAllAndLastReportAsync(Completed(42), ct);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_reply_spelled_differently_updates_the_configured_target()
    {
        var ct = TestContext.Current.CancellationToken;
        await StartedWorkerAsync(ct);

        var report = await AnswerAllAndLastReportAsync(
            new FetchDomainCompleted(new Location("LUCERNE", 0, 0), DomainKind.Forecast, 42), ct);

        Assert.Multiple(
            () => Assert.Equal(_target, report.Target),
            () => Assert.Equal(_lucerne, report.State.Location),
            () => Assert.NotNull(report.State.LastChangeUtc));
    }

    // --- Triggers ---

    [Fact(Timeout = 30_000)]
    public async Task A_poll_asks_only_the_configured_domains()
    {
        var ct = TestContext.Current.CancellationToken;
        IReadOnlyList<DomainKind> domains = [DomainKind.Forecast, DomainKind.Flood];
        var worker = CreateWorker(domains);

        worker.Tell(new StartPolling(_target, _intervals));

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
        foreach (var _ in _enabledDomains)
        {
            var request = await ExpectRequestAsync(ct);
            inFlight.Add((_gateway.LastSender, request.Domain));
        }

        worker.Tell(new TriggerLocationPoll(TargetId));
        await _gateway.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), ct);

        foreach (var (sender, domain) in inFlight)
        {
            sender.Tell(ForDomain(Completed(42), domain));
        }

        await DrainReportsAsync(_enabledDomains.Count, ct);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_trigger_after_the_outcome_reaches_the_gateway()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAllAsync(Completed(42), ct);

        worker.Tell(new TriggerLocationPoll(TargetId));

        var request = await ExpectRequestAsync(ct);
        Assert.Equal(_target, new PollTarget(request.Location));
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

    // --- Domain scheduling ---

    [Fact(Timeout = 30_000)]
    public async Task A_restarted_worker_polls_only_the_domains_that_are_due()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);

        foreach (var _ in _enabledDomains)
        {
            var request = await ExpectRequestAsync(ct);
            _gateway.LastSender.Tell(request.Domain == DomainKind.Forecast
                ? new FetchDomainCompleted(_lucerne, DomainKind.Forecast, 1)
                : new FetchDomainFailed(_lucerne, request.Domain, PollFailureReason.Transport, "down"));
        }

        await DrainReportsAsync(_enabledDomains.Count, ct);
        await StopAsync(worker, ct);

        _time.Advance(TimeSpan.FromMinutes(2));
        CreateWorker();

        await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        var next = await ExpectRequestAsync(ct);
        Assert.Equal(DomainKind.AirQuality, next.Domain);
        await _gateway.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(500), ct);
    }

    [Fact(Timeout = 30_000)]
    public async Task Per_domain_intervals_survive_a_restart()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAllAsync(Completed(1), ct);

        var expectedNext = _time.GetUtcNow() + _forecastInterval - DomainPollStateExtensions.SteadyBuffer;
        await StopAsync(worker, ct);
        CreateWorker();

        var recovered = await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        Assert.Equal(expectedNext, recovered.State.NextPollUtc);
        await _gateway.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(500), ct);
    }
}
