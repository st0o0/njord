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
public sealed class PollWorkerCoordinatesTests : Akka.Hosting.TestKit.TestKit
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

    private async Task StopAsync(IActorRef worker, CancellationToken ct)
    {
        await WatchAsync(worker);
        await worker.GracefulStop(TimeSpan.FromSeconds(15));
        await ExpectTerminatedAsync(worker, cancellationToken: ct);
    }

    private static readonly Location Moved = new("lucerne", 47.10, 8.40);
    private static (string, double, double) Coordinates(Location l) => (l.Name, l.Latitude, l.Longitude);

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
    public async Task A_restart_with_changed_coordinates_retargets_and_keeps_the_poll_state()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        var before = await AnswerAllAndLastReportAsync(Completed(1), ct);

        worker.Tell(new StartPolling(new PollTarget(Moved), Discovery));

        var after = await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        Assert.Multiple(
            () => Assert.Equal(Coordinates(Moved), Coordinates(after.Target.Location)),
            () => Assert.Equal(before.State.LastChangeUtc, after.State.LastChangeUtc),
            () => Assert.Equal(before.State.Phase, after.State.Phase));

        _time.Advance(TimeSpan.FromHours(1));
        worker.Tell(new TriggerLocationPoll(TargetId));
        var request = await ExpectRequestAsync(ct);
        Assert.Equal(Coordinates(Moved), Coordinates(request.Location));
    }

    [Fact(Timeout = 30_000)]
    public async Task A_restart_with_changed_coordinates_survives_a_recovery()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        worker.Tell(new StartPolling(new PollTarget(Moved), Discovery));
        await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        await StopAsync(worker, ct);

        CreateWorker();

        var report = await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        Assert.Equal(Coordinates(Moved), Coordinates(report.Target.Location));
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
}
