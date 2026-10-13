using Akka.Actor;
using Akka.Event;
using Akka.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;
using Njord.Core.Health;
using Njord.Messages.Common;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;
using Njord.Weather;

namespace Njord.Pipeline.Tests;

public sealed class PollSchedulerSpec : NjordActorSpec
{
    private readonly FakeTimeProvider _time = TestTimeouts.AtEpoch();
    private Akka.TestKit.TestProbe _offerProbe = null!;

    protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        var options = new NjordOptions
        {
            Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2"],
        };
        services.AddSingleton<TimeProvider>(_time);
        services.AddSingleton(Options.Create(options));
        services.AddSingleton(Options.Create(new PollingOptions { DiscoveryInterval = TimeSpan.FromMilliseconds(50) }));
        services.AddSingleton(new NjordHealthMonitor { ServiceStartedUtc = _time.GetUtcNow() });
    }

    protected override void ConfigureActors(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder
            .WithActors((system, registry) =>
            {
                _offerProbe = CreateTestProbe();
                var fakeGateway = system.ActorOf(
                    Props.Create(() => new FakeGatewayActor(_offerProbe)));
                registry.Register<IForecastGateway>(fakeGateway);
            })
            .WithActors((system, registry, resolver) =>
            {
                registry.Register<IPollScheduler>(
                    system.ActorOf(resolver.Props<PollScheduler>(), "scheduler"));
            });
    }

    private IActorRef Scheduler => GetActor<IPollScheduler>();

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Scheduler_sends_fetch_to_gateway_on_startup()
    {
        var msg = await _offerProbe.ExpectMsgAsync<FetchForecast>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("lucerne", msg.Location.Name);
        Assert.Equal("icon_d2", msg.ModelId);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Hash_change_triggers_ack_response()
    {
        await _offerProbe.ExpectMsgAsync<FetchForecast>(cancellationToken: TestContext.Current.CancellationToken);

        await Scheduler.Ask<Ack>(new FetchForecastCompleted(new LocationId("lucerne"), "icon_d2", 42), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Unchanged_hash_also_acks()
    {
        await _offerProbe.ExpectMsgAsync<FetchForecast>(cancellationToken: TestContext.Current.CancellationToken);

        await Scheduler.Ask<Ack>(new FetchForecastCompleted(new LocationId("lucerne"), "icon_d2", 42), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        await Scheduler.Ask<Ack>(new FetchForecastCompleted(new LocationId("lucerne"), "icon_d2", 42), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Transport_failure_does_not_crash_and_allows_immediate_repoll()
    {
        await _offerProbe.ExpectMsgAsync<FetchForecast>(cancellationToken: TestContext.Current.CancellationToken);

        Scheduler.Tell(new FetchForecastFailed(new LocationId("lucerne"), "icon_d2", PollFailureReason.Transport, "test"));

        var result = await Scheduler.Ask<TriggerImmediatePollCompleted>(
            new TriggerImmediatePoll(new LocationId("lucerne"), "icon_d2"), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.Equal(1, result.Count);
        await _offerProbe.ExpectMsgAsync<FetchForecast>(cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Rate_limited_failure_does_not_crash_and_allows_immediate_repoll()
    {
        await _offerProbe.ExpectMsgAsync<FetchForecast>(cancellationToken: TestContext.Current.CancellationToken);

        Scheduler.Tell(new FetchForecastFailed(new LocationId("lucerne"), "icon_d2", PollFailureReason.RateLimited, "test"));

        var result = await Scheduler.Ask<TriggerImmediatePollCompleted>(
            new TriggerImmediatePoll(new LocationId("lucerne"), "icon_d2"), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.Equal(1, result.Count);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Model_unavailable_does_not_crash_and_allows_immediate_repoll()
    {
        await _offerProbe.ExpectMsgAsync<FetchForecast>(cancellationToken: TestContext.Current.CancellationToken);

        Scheduler.Tell(new FetchForecastFailed(new LocationId("lucerne"), "icon_d2", PollFailureReason.ModelUnavailable, "test"));

        var result = await Scheduler.Ask<TriggerImmediatePollCompleted>(
            new TriggerImmediatePoll(new LocationId("lucerne"), "icon_d2"), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.Equal(1, result.Count);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Malformed_payload_does_not_crash_and_allows_immediate_repoll()
    {
        await _offerProbe.ExpectMsgAsync<FetchForecast>(cancellationToken: TestContext.Current.CancellationToken);

        Scheduler.Tell(new FetchForecastFailed(new LocationId("lucerne"), "icon_d2", PollFailureReason.MalformedPayload, "test"));

        var result = await Scheduler.Ask<TriggerImmediatePollCompleted>(
            new TriggerImmediatePoll(new LocationId("lucerne"), "icon_d2"), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.Equal(1, result.Count);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Trigger_immediate_poll_for_all_returns_all_targets()
    {
        await _offerProbe.ExpectMsgAsync<FetchForecast>(cancellationToken: TestContext.Current.CancellationToken);

        var result = await Scheduler.Ask<TriggerImmediatePollCompleted>(
            new TriggerImmediatePoll(null, ""), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Count);
        Assert.Contains("lucerne/icon_d2", result.Targets);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Trigger_immediate_poll_for_unknown_location_returns_zero()
    {
        await _offerProbe.ExpectMsgAsync<FetchForecast>(cancellationToken: TestContext.Current.CancellationToken);

        var result = await Scheduler.Ask<TriggerImmediatePollCompleted>(
            new TriggerImmediatePoll(new LocationId("nonexistent"), ""), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Equal(0, result.Count);
        Assert.Empty(result.Targets);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Trigger_immediate_poll_actually_sends_fetch_to_gateway()
    {
        await _offerProbe.ExpectMsgAsync<FetchForecast>(cancellationToken: TestContext.Current.CancellationToken);

        await Scheduler.Ask<TriggerImmediatePollCompleted>(
            new TriggerImmediatePoll(new LocationId("lucerne"), "icon_d2"), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        var latest = await _offerProbe.ExpectMsgAsync<FetchForecast>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("lucerne", latest.Location.Name);
        Assert.Equal("icon_d2", latest.ModelId);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Gateway_termination_does_not_busy_loop_when_no_replacement_is_registered()
    {
        await _offerProbe.ExpectMsgAsync<FetchForecast>(cancellationToken: TestContext.Current.CancellationToken);

        var warningProbe = CreateTestProbe();
        Sys.EventStream.Subscribe(warningProbe, typeof(Warning));

        var gateway = await ActorRegistry.GetAsync<IForecastGateway>(TestContext.Current.CancellationToken);
        await WatchAsync(gateway);
        await gateway.GracefulStop(TimeSpan.FromSeconds(2));
        await ExpectTerminatedAsync(gateway, cancellationToken: TestContext.Current.CancellationToken);

        await AwaitConditionAsync(() => warningProbe.HasMessages, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(100), cancellationToken: TestContext.Current.CancellationToken);

        var terminatedWarnings = 0;
        while (warningProbe.HasMessages)
        {
            var msg = await warningProbe.ReceiveOneAsync(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
            if (msg is Warning { Message: var message } && message?.ToString()?.Contains("Gateway terminated") == true)
            {
                terminatedWarnings++;
            }
        }

        Assert.True(terminatedWarnings <= 1,
            $"Expected at most 1 'Gateway terminated' warning but got {terminatedWarnings} — possible tight loop");
    }

    private sealed class FakeGatewayActor : ReceiveActor
    {
        public FakeGatewayActor(IActorRef probe)
        {
            Receive<FetchForecast>(msg => probe.Tell(msg));
        }
    }
}
