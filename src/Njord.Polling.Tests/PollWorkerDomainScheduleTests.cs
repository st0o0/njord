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
public sealed class PollWorkerDomainScheduleTests : Akka.Hosting.TestKit.TestKit
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
    public async Task A_restarted_worker_polls_only_the_domains_that_are_due()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);

        // Forecast answers, AirQuality fails: weather waits a discovery interval, air quality retries after a minute.
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

        // Three changes at 1 h intervals to reach Steady (domain needs two consistent intervals).
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
