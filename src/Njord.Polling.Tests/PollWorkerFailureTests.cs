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

// The worker is created with explicit constructor arguments; a probe plays the gateway (it answers through LastSender)
// and another probe plays the poll manager.
public sealed class PollWorkerFailureTests : Akka.Hosting.TestKit.TestKit
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

    [Theory(Timeout = 30_000)]
    [InlineData(PollFailureReason.Transport, 60)]
    [InlineData(PollFailureReason.ModelUnavailable, 60)]
    [InlineData(PollFailureReason.MalformedPayload, 60)]
    [InlineData(PollFailureReason.RateLimited, 300)]
    public async Task A_failure_is_reported_with_its_backoff(PollFailureReason reason, int delaySeconds)
    {
        var ct = TestContext.Current.CancellationToken;
        await StartedWorkerAsync(ct);

        var report = await AnswerAllAndLastReportAsync(
            new FetchDomainFailed(Lucerne, DomainKind.Forecast, reason, "simulated"), ct);

        Assert.Equal(_time.GetUtcNow() + TimeSpan.FromSeconds(delaySeconds), report.State.NextPollUtc);
    }

    [Theory(Timeout = 30_000)]
    [InlineData(FetchRejectionReason.QueueFull, 60)]
    [InlineData(FetchRejectionReason.PipelineUnavailable, 5)]
    public async Task A_rejection_is_reported_as_failed_with_its_backoff(FetchRejectionReason reason, int delaySeconds)
    {
        var ct = TestContext.Current.CancellationToken;
        await StartedWorkerAsync(ct);

        var report = await AnswerAllAndLastReportAsync(
            new FetchDomainRejected(Lucerne, DomainKind.Forecast, reason), ct);

        Assert.Equal(_time.GetUtcNow() + TimeSpan.FromSeconds(delaySeconds), report.State.NextPollUtc);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_pipeline_unavailable_rejection_retries_soon()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAllAsync(new FetchDomainFailed(Lucerne, DomainKind.Forecast, PollFailureReason.Transport, "simulated"), ct);

        worker.Tell(new TriggerLocationPoll(TargetId));
        var deferred = await AnswerAllAndLastReportAsync(
            new FetchDomainRejected(Lucerne, DomainKind.Forecast, FetchRejectionReason.PipelineUnavailable), ct);

        // The pipeline-unavailable rejection leaves the failure count at one, so the next failure is the second and backs off two minutes.
        worker.Tell(new TriggerLocationPoll(TargetId));
        var queueFull = await AnswerAllAndLastReportAsync(
            new FetchDomainRejected(Lucerne, DomainKind.Forecast, FetchRejectionReason.QueueFull), ct);

        Assert.Multiple(
            () => Assert.Equal(_time.GetUtcNow() + PollWorkerState.PipelineUnavailableRetry, deferred.State.NextPollUtc),
            () => Assert.Equal(_time.GetUtcNow() + TimeSpan.FromMinutes(2), queueFull.State.NextPollUtc));
    }

    private async Task<PollWorkerReport> FailOnceAsync(IActorRef worker, CancellationToken ct)
    {
        worker.Tell(new TriggerLocationPoll(TargetId));
        return await AnswerAllAndLastReportAsync(
            new FetchDomainFailed(Lucerne, DomainKind.Forecast, PollFailureReason.Transport, "simulated"), ct);
    }

    [Fact(Timeout = 30_000)]
    public async Task The_third_consecutive_failure_backs_off_four_minutes()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAllAsync(new FetchDomainFailed(Lucerne, DomainKind.Forecast, PollFailureReason.Transport, "simulated"), ct);
        await FailOnceAsync(worker, ct);

        var third = await FailOnceAsync(worker, ct);

        Assert.Equal(_time.GetUtcNow() + TimeSpan.FromMinutes(4), third.State.NextPollUtc);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_success_resets_the_backoff()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAllAsync(new FetchDomainFailed(Lucerne, DomainKind.Forecast, PollFailureReason.Transport, "simulated"), ct);
        await FailOnceAsync(worker, ct);
        await FailOnceAsync(worker, ct);

        worker.Tell(new TriggerLocationPoll(TargetId));
        await AnswerAllAsync(Completed(1), ct);
        var failed = await FailOnceAsync(worker, ct);

        Assert.Equal(_time.GetUtcNow() + TimeSpan.FromSeconds(60), failed.State.NextPollUtc);
    }

    [Fact(Timeout = 30_000)]
    public async Task The_backoff_is_kept_across_a_restart()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAllAsync(new FetchDomainFailed(Lucerne, DomainKind.Forecast, PollFailureReason.Transport, "simulated"), ct);
        for (var i = 0; i < 3; i++)
        {
            await FailOnceAsync(worker, ct);
        }

        await StopAsync(worker, ct);
        var recovered = CreateWorker();
        await ExpectReportAsync(PollWorkerOutcome.Planned, ct);

        var next = await FailOnceAsync(recovered, ct);

        Assert.Equal(_time.GetUtcNow() + TimeSpan.FromMinutes(16), next.State.NextPollUtc);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_rate_limit_after_several_failures_keeps_the_longer_backoff()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAllAsync(new FetchDomainFailed(Lucerne, DomainKind.Forecast, PollFailureReason.Transport, "simulated"), ct);
        for (var i = 0; i < 4; i++)
        {
            await FailOnceAsync(worker, ct);
        }

        worker.Tell(new TriggerLocationPoll(TargetId));
        var report = await AnswerAllAndLastReportAsync(
            new FetchDomainFailed(Lucerne, DomainKind.Forecast, PollFailureReason.RateLimited, "simulated"), ct);

        Assert.Equal(_time.GetUtcNow() + TimeSpan.FromMinutes(32), report.State.NextPollUtc);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_pipeline_unavailable_rejection_does_not_grow_the_backoff()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAllAsync(new FetchDomainRejected(Lucerne, DomainKind.Forecast, FetchRejectionReason.PipelineUnavailable), ct);
        worker.Tell(new TriggerLocationPoll(TargetId));
        await AnswerAllAsync(new FetchDomainRejected(Lucerne, DomainKind.Forecast, FetchRejectionReason.PipelineUnavailable), ct);

        var failed = await FailOnceAsync(worker, ct);

        Assert.Equal(_time.GetUtcNow() + TimeSpan.FromSeconds(60), failed.State.NextPollUtc);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_failing_domain_does_not_disturb_the_weather_domain_state()
    {
        var ct = TestContext.Current.CancellationToken;
        await StartedWorkerAsync(ct);

        foreach (var _ in EnabledDomains)
        {
            var request = await ExpectRequestAsync(ct);
            _gateway.LastSender.Tell(request.Domain == DomainKind.AirQuality
                ? new FetchDomainFailed(Lucerne, request.Domain, PollFailureReason.Transport, "simulated")
                : Completed(1));
        }

        // The report state follows the Forecast domain (last change, miss count) and the earliest due domain (next poll).
        var last = await DrainReportsAsync(EnabledDomains.Count, ct);

        Assert.Multiple(
            () => Assert.Equal(_time.GetUtcNow(), last.State.LastChangeUtc),
            () => Assert.Equal(0, last.State.MissCount),
            () => Assert.Equal(_time.GetUtcNow() + TimeSpan.FromSeconds(60), last.State.NextPollUtc));
    }
}
