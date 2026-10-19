using Akka.Actor;
using Akka.DependencyInjection;
using Akka.Hosting;
using Akka.TestKit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Njord.Core;
using Njord.Core.Actors;
using Njord.Core.Configuration.Polling;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Tests.Shared;

namespace Njord.Pipeline.Tests;

// Every request ends: a gateway that does not answer, a stopped gateway and a missing gateway all end in a
// Transport failure with the transient backoff, never in silence and never in a retry loop.
public sealed class PollWorkerFetchReplySpec : NjordActorSpec
{
    // Short enough that the Ask times out within the test; the defaults are covered by FetchTimeoutsSpec.
    private static readonly FetchTimeouts ShortTimeouts =
        new(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(300));

    private static readonly PollTarget Target = new(new Location("lucerne", 0, 0));

    private readonly FakeTimeProvider _time = TestTimeouts.AtEpoch();
    private TestProbe _manager = null!;
    private TestProbe _requests = null!;

    protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        services.AddSingleton<TimeProvider>(_time);
        services.AddSingleton(ShortTimeouts);
    }

    protected override void ConfigureActors(AkkaConfigurationBuilder builder, IServiceProvider provider) =>
        builder.WithActors((_, registry) =>
        {
            _manager = CreateTestProbe("manager");
            _requests = CreateTestProbe("requests");
            registry.Register<IPollManager>(_manager.Ref);
        });

    private void Start() =>
        Sys.ActorOf(DependencyResolver.For(Sys).Props<PollWorker>(Target.Id), $"worker-{Guid.NewGuid():N}")
            .Tell(new StartPolling(Target, TimeSpan.FromHours(1)));

    private async Task<PollWorkerReport> ExpectFailedReportAsync(CancellationToken ct)
    {
        var planned = await _manager.ExpectMsgAsync<PollWorkerReport>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        Assert.Equal(PollWorkerOutcome.Planned, planned.Outcome);
        var failed = await _manager.ExpectMsgAsync<PollWorkerReport>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        Assert.Equal(PollWorkerOutcome.Failed, failed.Outcome);
        return failed;
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_gateway_that_does_not_answer_ends_in_a_transient_backoff()
    {
        var ct = TestContext.Current.CancellationToken;
        ActorRegistry.Register<IForecastGateway>(Sys.ActorOf(Props.Create(() => new SilentGateway(_requests.Ref))));
        Start();

        await _requests.ExpectMsgAsync<FetchDomain>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        var report = await ExpectFailedReportAsync(ct);

        Assert.Equal(_time.GetUtcNow() + TimeSpan.FromMinutes(1), report.State.NextPollUtc);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_missing_gateway_ends_in_a_transient_backoff()
    {
        var ct = TestContext.Current.CancellationToken;
        Start();

        var report = await ExpectFailedReportAsync(ct);

        Assert.Equal(_time.GetUtcNow() + TimeSpan.FromMinutes(1), report.State.NextPollUtc);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_terminated_gateway_causes_one_failure_and_no_poll_loop()
    {
        var ct = TestContext.Current.CancellationToken;
        var gateway = Sys.ActorOf(Props.Create(() => new SilentGateway(_requests.Ref)));
        await WatchAsync(gateway);
        await gateway.GracefulStop(TestTimeouts.RealTimeWaitMax);
        await ExpectTerminatedAsync(gateway, cancellationToken: ct);
        ActorRegistry.Register<IForecastGateway>(gateway);

        Start();

        var report = await ExpectFailedReportAsync(ct);
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
