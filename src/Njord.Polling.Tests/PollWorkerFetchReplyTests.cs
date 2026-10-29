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

// Every request ends: a gateway that does not answer within the reply timer, a stopped gateway and a missing gateway all end in a
// Transport failure with the transient backoff, never in silence and never in a retry loop.
public sealed class PollWorkerFetchReplyTests : Akka.Hosting.TestKit.TestKit
{
    private static readonly TimeSpan ShortReplyTimeout = TimeSpan.FromMilliseconds(300);

    private static readonly IOptions<DomainRequirements> Requirements = Options.Create(TestRequirements.For(DomainKind.Forecast, DomainKind.AirQuality));

    private static readonly PollTarget Target = new(new Location("lucerne", 0, 0));
    private readonly FakeTimeProvider _time = new FakeTimeProvider(TestTimeConstants.PipelineEpoch);
    private TestProbe _manager = null!;
    private TestProbe _requests = null!;

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.AddTestPersistence();
        builder.WithActors((_, registry) =>
        {
            _manager = CreateTestProbe("manager");
            _requests = CreateTestProbe("requests");
            registry.Register<IPollManager>(_manager.Ref);
        });
    }

    private void Start() =>
        Sys.ActorOf(Props.Create(() => new PollWorker(_time, Options.Create(new PollingOptions()), Requirements, Target.Id, ShortReplyTimeout)),
                $"worker-{Guid.NewGuid():N}")
            .Tell(new StartPolling(Target, TimeSpan.FromHours(1)));

    private async Task<PollWorkerReport> ExpectFailedReportsAsync(CancellationToken ct)
    {
        var planned = await _manager.ExpectMsgAsync<PollWorkerReport>(TimeSpan.FromSeconds(5), cancellationToken: ct);
        Assert.Equal(PollWorkerOutcome.Planned, planned.Outcome);
        // Each domain produces its own Failed report; drain all of them and return the last.
        PollWorkerReport? last = null;
        for (var i = 0; i < 2; i++)
        {
            last = await _manager.ExpectMsgAsync<PollWorkerReport>(TimeSpan.FromSeconds(5), cancellationToken: ct);
            Assert.Equal(PollWorkerOutcome.Failed, last.Outcome);
        }

        return last!;
    }

    [Fact(Timeout = 30_000)]
    public async Task A_gateway_that_does_not_answer_ends_in_a_transient_backoff()
    {
        var ct = TestContext.Current.CancellationToken;
        ActorRegistry.Register<IForecastGateway>(Sys.ActorOf(Props.Create(() => new SilentGateway(_requests.Ref))));
        Start();

        await _requests.ExpectMsgAsync<FetchDomain>(TimeSpan.FromSeconds(5), cancellationToken: ct);
        var report = await ExpectFailedReportsAsync(ct);

        Assert.Equal(_time.GetUtcNow() + TimeSpan.FromMinutes(1), report.State.NextPollUtc);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_missing_gateway_ends_in_a_transient_backoff()
    {
        var ct = TestContext.Current.CancellationToken;
        Start();

        var report = await ExpectFailedReportsAsync(ct);

        Assert.Equal(_time.GetUtcNow() + TimeSpan.FromMinutes(1), report.State.NextPollUtc);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_terminated_gateway_causes_one_failure_and_no_poll_loop()
    {
        var ct = TestContext.Current.CancellationToken;
        var gateway = Sys.ActorOf(Props.Create(() => new SilentGateway(_requests.Ref)));
        await WatchAsync(gateway);
        await gateway.GracefulStop(TimeSpan.FromSeconds(15));
        await ExpectTerminatedAsync(gateway, cancellationToken: ct);
        ActorRegistry.Register<IForecastGateway>(gateway);

        Start();

        var report = await ExpectFailedReportsAsync(ct);
        Assert.Equal(_time.GetUtcNow() + TimeSpan.FromMinutes(1), report.State.NextPollUtc);
        await _manager.ExpectNoMsgAsync(TimeSpan.FromSeconds(1), ct);
    }

    private sealed class SilentGateway : ReceiveActor
    {
        public SilentGateway(IActorRef requests)
        {
            Receive<FetchDomain>(requests.Tell);
        }
    }
}
