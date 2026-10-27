using Akka.Actor;
using Akka.Hosting;
using Akka.TestKit;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Core.Domain;
using Njord.Core.Polling;
using Njord.Messages.Forecast;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Polling.Tests;

// The worker is created with explicit constructor arguments; a probe plays the gateway (it answers through LastSender)
// and another probe plays the poll manager.
public sealed class PollWorkerOutcomeTests : Akka.Hosting.TestKit.TestKit
{
    private const string TargetId = "lucerne";
    private static readonly TimeSpan Discovery = TimeSpan.FromMinutes(20);
    private static readonly Location Lucerne = new("lucerne", 47.05, 8.31);
    private static readonly PollTarget Target = new(Lucerne);
    private static readonly IReadOnlyList<DomainKind> EnabledDomains = [DomainKind.Forecast, DomainKind.AirQuality];

    private static IOptions<FeatureRequirementsOptions> RequirementsFor(IReadOnlyList<DomainKind> domains) =>
        Options.Create(new FeatureRequirementsOptions { Domains = domains });

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

    // Per-domain cycle learning needs three consistent changes (two consistent intervals).
    // After the second change the domain stays in Discovery with a candidate cycle.
    // After the third consistent change it transitions to Steady.
    [Fact(Timeout = 30_000)]
    public async Task Three_consistent_changes_learn_the_cycle()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);

        // First change
        await AnswerAllAsync(Completed(1), ct);

        // Second change at +3h - records a candidate but stays Discovery
        _time.Advance(TimeSpan.FromHours(3));
        worker.Tell(new TriggerLocationPoll(TargetId));
        await AnswerAllAsync(Completed(2), ct);

        // Third consistent change at +6h - transitions to Steady
        _time.Advance(TimeSpan.FromHours(3));
        worker.Tell(new TriggerLocationPoll(TargetId));
        // Drain all requests and collect the last report to check the Forecast domain state.
        var reports = new List<PollWorkerReport>();
        foreach (var _ in EnabledDomains)
        {
            var request = await ExpectRequestAsync(ct);
            _gateway.LastSender.Tell(ForDomain(Completed(3), request.Domain));
        }

        for (var i = 0; i < EnabledDomains.Count; i++)
        {
            reports.Add(await _manager.ExpectMsgAsync<PollWorkerReport>(TimeSpan.FromSeconds(5), cancellationToken: ct));
        }

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

        var report = await AnswerAllAndLastReportAsync(new FetchDomainCompleted(new Location("LUCERNE", 0, 0), DomainKind.Forecast, 42), ct);

        Assert.Multiple(
            () => Assert.Equal(Target, report.Target),
            () => Assert.Equal(Lucerne, report.State.Location),
            () => Assert.NotNull(report.State.LastChangeUtc));
    }
}
