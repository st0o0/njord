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
public sealed class PollWorkerTriggerTests : Akka.Hosting.TestKit.TestKit
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
    public async Task A_poll_asks_only_the_configured_domains()
    {
        var ct = TestContext.Current.CancellationToken;
        IReadOnlyList<DomainKind> domains = [DomainKind.Forecast, DomainKind.Flood];
        var worker = Sys.ActorOf(
            Props.Create(() => new PollWorker(_time, Options.Create(new PollingOptions()), RequirementsFor(domains), TargetId)),
            $"worker-{Guid.NewGuid():N}");

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

        // The requests in flight still end normally and their outcome is reported per domain.
        foreach (var (sender, domain) in inFlight)
        {
            sender.Tell(ForDomain(Completed(42), domain));
        }

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
}
