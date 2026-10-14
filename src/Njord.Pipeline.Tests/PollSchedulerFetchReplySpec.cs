using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;
using Njord.Core.Health;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;
using Njord.Weather;

namespace Njord.Pipeline.Tests;

public sealed class PollSchedulerFetchReplySpec : NjordActorSpec
{
    // Short enough that the Ask times out within the test; the defaults are covered by FetchTimeoutsSpec.
    private static readonly FetchTimeouts ShortTimeouts =
        new(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(300));

    private readonly FakeTimeProvider _time = TestTimeouts.AtEpoch();
    private readonly ScriptedGateway _script = new();
    private Akka.TestKit.TestProbe _requests = null!;

    protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        var options = new NjordOptions
        {
            Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2"],
        };
        services.AddSingleton<TimeProvider>(_time);
        services.AddSingleton(Options.Create(options));
        services.AddSingleton(Options.Create(new PollingOptions { DiscoveryInterval = TimeSpan.FromHours(1) }));
        services.AddSingleton(new NjordHealthMonitor { ServiceStartedUtc = _time.GetUtcNow() });
        services.AddSingleton(ShortTimeouts);
    }

    protected override void ConfigureActors(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder
            .WithActors((system, registry) =>
            {
                _requests = CreateTestProbe();
                registry.Register<IForecastGateway>(system.ActorOf(Props.Create(() => new ScriptedGatewayActor(_script, _requests.Ref))));
            })
            .WithActors((system, registry, resolver) =>
                registry.Register<IPollScheduler>(system.ActorOf(resolver.Props<PollScheduler>(), "scheduler")));
    }

    private IActorRef Scheduler => GetActor<IPollScheduler>();

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_gateway_that_does_not_answer_ends_in_a_transient_backoff()
    {
        var ct = TestContext.Current.CancellationToken;
        await _requests.ExpectMsgAsync<FetchForecast>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);

        await AssertNextPollAsync(_time.GetUtcNow() + TimeSpan.FromMinutes(1), ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_budget_rejection_backs_off_at_least_five_minutes()
    {
        var ct = TestContext.Current.CancellationToken;
        // The startup poll is not answered and times out first (one transient failure, next poll in 1 min).
        await _requests.ExpectMsgAsync<FetchForecast>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        await AssertNextPollAsync(_time.GetUtcNow() + TimeSpan.FromMinutes(1), ct);
        _script.Reply = FetchRejectionReason.Budget;

        await Scheduler.Ask<TriggerImmediatePollCompleted>(
            new TriggerImmediatePoll(new LocationId("lucerne"), "icon_d2"), TestTimeouts.RealTimeWaitMax, ct);

        await AssertNextPollAsync(_time.GetUtcNow() + TimeSpan.FromMinutes(5), ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_queue_full_rejection_is_retried_with_the_transient_backoff()
    {
        var ct = TestContext.Current.CancellationToken;
        // The startup poll is not answered and times out first (one transient failure, next poll in 1 min).
        await _requests.ExpectMsgAsync<FetchForecast>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        await AssertNextPollAsync(_time.GetUtcNow() + TimeSpan.FromMinutes(1), ct);
        _script.Reply = FetchRejectionReason.QueueFull;

        await Scheduler.Ask<TriggerImmediatePollCompleted>(
            new TriggerImmediatePoll(new LocationId("lucerne"), "icon_d2"), TestTimeouts.RealTimeWaitMax, ct);

        await AssertNextPollAsync(_time.GetUtcNow() + TimeSpan.FromMinutes(2), ct);
    }

    private async Task AssertNextPollAsync(DateTimeOffset expected, CancellationToken ct) =>
        await AwaitAssertAsync(async () =>
        {
            var states = await Scheduler.Ask<PollStatesResult>(new QueryPollStates(), TestTimeouts.RealTimeWaitMax, ct);
            var entry = Assert.Single(states.Entries);
            Assert.Equal(expected, entry.NextPollUtc);
        }, TestTimeouts.AwaitAssertMax, TimeSpan.FromMilliseconds(50), cancellationToken: ct);

    private sealed class ScriptedGateway
    {
        public volatile object? Reply;
    }

    // Reports every request; answers with a rejection of the scripted reason, or never when none is scripted.
    private sealed class ScriptedGatewayActor : ReceiveActor
    {
        public ScriptedGatewayActor(ScriptedGateway script, IActorRef requests)
        {
            Receive<FetchForecast>(msg =>
            {
                requests.Tell(msg);
                if (script.Reply is FetchRejectionReason reason)
                {
                    Sender.Tell(new FetchForecastRejected(msg.Location, msg.ModelId, reason));
                }
            });
        }
    }
}
