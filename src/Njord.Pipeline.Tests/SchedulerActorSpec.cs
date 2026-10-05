using Akka.Actor;
using Akka.Event;
using Akka.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Actors;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Health;
using Njord.Messages.Common;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Pipeline.Tests;

public sealed class SchedulerActorSpec : Akka.Hosting.TestKit.TestKit
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 7, 12, 6, 0, 0, TimeSpan.Zero));
    private Akka.TestKit.TestProbe _offerProbe = null!;

    protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        var options = new NjordOptions
        {
            DiscoveryInterval = TimeSpan.FromMilliseconds(50),
            Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2"],
        };
        services.AddSingleton<TimeProvider>(_time);
        services.AddSingleton(Options.Create(options));
        services.AddSingleton(ParameterRegistry.Resolve(["Weather"], [], []));
        services.AddSingleton(new NjordHealthState { ServiceStartedUtc = _time.GetUtcNow() });
    }

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder
            .AddTestPersistence()
            .WithActors((system, registry) =>
            {
                _offerProbe = CreateTestProbe();
                var fakePipeline = system.ActorOf(
                    Props.Create(() => new FakePipelineActor(_offerProbe)));
                registry.Register<IPipelineActor>(fakePipeline);
            })
            .WithActors((system, registry, resolver) =>
            {
                registry.Register<ISchedulerActor>(
                    system.ActorOf(resolver.Props<SchedulerActor>(), "scheduler"));
            })
            .AddTestTimefactor();
    }

    private IActorRef Scheduler => ActorRegistry.Get<ISchedulerActor>();

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Scheduler_sends_target_to_pipeline_on_startup()
    {
        var target = await _offerProbe.ExpectMsgAsync<WeightedTarget>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("lucerne", target.Location.Name);
        Assert.Equal("icon_d2", target.Model.Id);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Hash_change_triggers_ack_response()
    {
        await _offerProbe.ExpectMsgAsync<WeightedTarget>(cancellationToken: TestContext.Current.CancellationToken);

        var result = await Scheduler.Ask<Ack>(new HashResult("lucerne", "icon_d2", 42), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.NotNull(result);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Unchanged_hash_also_acks()
    {
        await _offerProbe.ExpectMsgAsync<WeightedTarget>(cancellationToken: TestContext.Current.CancellationToken);

        await Scheduler.Ask<Ack>(new HashResult("lucerne", "icon_d2", 42), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        var result = await Scheduler.Ask<Ack>(new HashResult("lucerne", "icon_d2", 42), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.NotNull(result);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Transport_failure_does_not_crash_and_allows_immediate_repoll()
    {
        await _offerProbe.ExpectMsgAsync<WeightedTarget>(cancellationToken: TestContext.Current.CancellationToken);

        Scheduler.Tell(new FetchFailed("lucerne", "icon_d2", FetchFailureReason.Transport, "test"));

        var result = await Scheduler.Ask<TriggerImmediatePollResult>(
            new TriggerImmediatePoll("lucerne", "icon_d2"), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.Equal(1, result.Count);
        await _offerProbe.ExpectMsgAsync<WeightedTarget>(cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Rate_limited_failure_does_not_crash_and_allows_immediate_repoll()
    {
        await _offerProbe.ExpectMsgAsync<WeightedTarget>(cancellationToken: TestContext.Current.CancellationToken);

        Scheduler.Tell(new FetchFailed("lucerne", "icon_d2", FetchFailureReason.RateLimited, "test"));

        var result = await Scheduler.Ask<TriggerImmediatePollResult>(
            new TriggerImmediatePoll("lucerne", "icon_d2"), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.Equal(1, result.Count);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Model_unavailable_does_not_crash_and_allows_immediate_repoll()
    {
        await _offerProbe.ExpectMsgAsync<WeightedTarget>(cancellationToken: TestContext.Current.CancellationToken);

        Scheduler.Tell(new FetchFailed("lucerne", "icon_d2", FetchFailureReason.ModelUnavailable, "test"));

        var result = await Scheduler.Ask<TriggerImmediatePollResult>(
            new TriggerImmediatePoll("lucerne", "icon_d2"), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.Equal(1, result.Count);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Malformed_payload_does_not_crash_and_allows_immediate_repoll()
    {
        await _offerProbe.ExpectMsgAsync<WeightedTarget>(cancellationToken: TestContext.Current.CancellationToken);

        Scheduler.Tell(new FetchFailed("lucerne", "icon_d2", FetchFailureReason.MalformedPayload, "test"));

        var result = await Scheduler.Ask<TriggerImmediatePollResult>(
            new TriggerImmediatePoll("lucerne", "icon_d2"), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.Equal(1, result.Count);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Trigger_immediate_poll_for_all_returns_all_targets()
    {
        await _offerProbe.ExpectMsgAsync<WeightedTarget>(cancellationToken: TestContext.Current.CancellationToken);

        var result = await Scheduler.Ask<TriggerImmediatePollResult>(
            new TriggerImmediatePoll("", ""), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Count);
        Assert.Contains("lucerne/icon_d2", result.Targets);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Trigger_immediate_poll_for_unknown_location_returns_zero()
    {
        await _offerProbe.ExpectMsgAsync<WeightedTarget>(cancellationToken: TestContext.Current.CancellationToken);

        var result = await Scheduler.Ask<TriggerImmediatePollResult>(
            new TriggerImmediatePoll("nonexistent", ""), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Equal(0, result.Count);
        Assert.Empty(result.Targets);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Trigger_immediate_poll_actually_offers_target_to_pipeline()
    {
        await _offerProbe.ExpectMsgAsync<WeightedTarget>(cancellationToken: TestContext.Current.CancellationToken);

        await Scheduler.Ask<TriggerImmediatePollResult>(
            new TriggerImmediatePoll("lucerne", "icon_d2"), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        var latest = await _offerProbe.ExpectMsgAsync<WeightedTarget>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("lucerne", latest.Location.Name);
        Assert.Equal("icon_d2", latest.Model.Id);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Pipeline_termination_does_not_busy_loop_when_no_replacement_is_registered()
    {
        await _offerProbe.ExpectMsgAsync<WeightedTarget>(cancellationToken: TestContext.Current.CancellationToken);

        // Subscribe to Warning log events to count tight-loop iterations. Each
        // loop iteration logs "PipelineActor terminated - waiting for new ref".
        var warningProbe = CreateTestProbe();
        Sys.EventStream.Subscribe(warningProbe, typeof(Warning));

        // Act: stop the pipeline with no replacement registered. The scheduler
        // will keep resolving ActorRegistry.Get<IPipelineActor>(), which keeps
        // returning the same (now-dead) ref, and should back off rather than
        // spin in a tight watch/Terminated loop.
        var pipeline = ActorRegistry.Get<IPipelineActor>();
        Watch(pipeline);
        await pipeline.GracefulStop(TimeSpan.FromSeconds(2));
        await ExpectTerminatedAsync(pipeline, cancellationToken: TestContext.Current.CancellationToken);

        await AwaitConditionAsync(() => warningProbe.HasMessages, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(100), cancellationToken: TestContext.Current.CancellationToken);

        var terminatedWarnings = 0;
        while (warningProbe.HasMessages)
        {
            var msg = warningProbe.ReceiveOne(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
            if (msg is Warning { Message: var message } && message?.ToString()?.Contains("PipelineActor terminated") == true)
            {
                terminatedWarnings++;
            }
        }

        Assert.True(terminatedWarnings <= 1,
            $"Expected at most 1 'PipelineActor terminated' warning but got {terminatedWarnings} — possible tight loop");
    }

    private sealed class FakePipelineActor : ReceiveActor
    {
        public FakePipelineActor(IActorRef probe)
        {
            Receive<WeightedTarget>(msg => probe.Tell(msg));
        }
    }
}
