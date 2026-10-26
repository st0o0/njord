using Akka.Actor;
using Akka.Hosting;
using Akka.TestKit;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Core.Polling;
using Njord.Forecast;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Polling.Tests;

// The worker is created with explicit constructor arguments; a probe plays the gateway (it answers through LastSender)
// and another probe plays the poll manager.
public abstract class PollWorkerSpecBase : NjordActorSpec
{
    protected const string TargetId = "lucerne";
    protected static readonly TimeSpan Discovery = TimeSpan.FromMinutes(20);
    protected static readonly Location Lucerne = new("lucerne", 47.05, 8.31);
    protected static readonly PollTarget Target = new(Lucerne);

    protected static readonly IReadOnlyList<DomainKind> EnabledDomains = [DomainKind.Weather, DomainKind.AirQuality];

    protected static IOptions<FeatureRequirementsOptions> RequirementsFor(IReadOnlyList<DomainKind> domains) =>
        Options.Create(new FeatureRequirementsOptions { Domains = domains });

    protected readonly FakeTimeProvider _time = TestTimeouts.AtEpoch();
    protected TestProbe _gateway = null!;
    protected TestProbe _manager = null!;

    protected override void ConfigureActors(AkkaConfigurationBuilder builder, IServiceProvider provider) =>
        builder.WithActors((_, registry) =>
        {
            _gateway = CreateTestProbe("gateway");
            _manager = CreateTestProbe("manager");
            registry.Register<IForecastGateway>(_gateway.Ref);
            registry.Register<IPollManager>(_manager.Ref);
        });

    private Props WorkerProps(string entityId) =>
        Props.Create(() => new PollWorker(_time, Options.Create(new PollingOptions()), RequirementsFor(EnabledDomains), entityId));

    protected IActorRef CreateWorker(string entityId = TargetId) =>
        Sys.ActorOf(WorkerProps(entityId), $"worker-{Guid.NewGuid():N}");

    // A child of the test actor: it plays the shard and receives the worker's Passivate request.
    protected IActorRef CreateShardedWorker(string entityId = TargetId) =>
        ChildActorOf(WorkerProps(entityId), $"worker-{Guid.NewGuid():N}");

    protected async Task<IActorRef> StartedWorkerAsync(CancellationToken ct)
    {
        var worker = CreateWorker();
        worker.Tell(new StartPolling(Target, Discovery));
        await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        return worker;
    }

    protected async Task<FetchDomain> ExpectRequestAsync(CancellationToken ct) =>
        await _gateway.ExpectMsgAsync<FetchDomain>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);

    // A poll asks every enabled domain independently; each one is answered with the same reply, retargeted to its domain.
    // Each domain produces its own report, so we drain all requests and all reports.
    protected async Task AnswerAllAsync(FetchDomainResponse reply, CancellationToken ct)
    {
        foreach (var _ in EnabledDomains)
        {
            var request = await ExpectRequestAsync(ct);
            _gateway.LastSender.Tell(ForDomain(reply, request.Domain));
        }

        await DrainReportsAsync(EnabledDomains.Count, ct);
    }

    // Answer all domain requests and return the last report (for backward-compatible assertions).
    protected async Task<PollWorkerReport> AnswerAllAndLastReportAsync(FetchDomainResponse reply, CancellationToken ct)
    {
        foreach (var _ in EnabledDomains)
        {
            var request = await ExpectRequestAsync(ct);
            _gateway.LastSender.Tell(ForDomain(reply, request.Domain));
        }

        return await DrainReportsAsync(EnabledDomains.Count, ct);
    }

    protected async Task<PollWorkerReport> DrainReportsAsync(int count, CancellationToken ct)
    {
        PollWorkerReport? last = null;
        for (var i = 0; i < count; i++)
        {
            last = await _manager.ExpectMsgAsync<PollWorkerReport>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        }

        return last!;
    }

    protected static FetchDomainResponse ForDomain(FetchDomainResponse reply, DomainKind domain) => reply switch
    {
        FetchDomainCompleted completed => completed with { Domain = domain },
        FetchDomainFailed failed => failed with { Domain = domain },
        FetchDomainRejected rejected => rejected with { Domain = domain },
        _ => reply,
    };

    protected async Task<PollWorkerReport> ExpectReportAsync(PollWorkerOutcome outcome, CancellationToken ct)
    {
        var report = await _manager.ExpectMsgAsync<PollWorkerReport>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        Assert.Equal(outcome, report.Outcome);
        return report;
    }

    protected static FetchDomainCompleted Completed(int hash) => new(Lucerne, DomainKind.Weather, hash);

    protected async Task StopAsync(IActorRef worker, CancellationToken ct)
    {
        await WatchAsync(worker);
        await worker.GracefulStop(TestTimeouts.RealTimeWaitMax);
        await ExpectTerminatedAsync(worker, cancellationToken: ct);
    }
}
