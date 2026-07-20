using Akka;
using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Health;
using Njord.Ingest;
using Njord.Pipeline;
using Njord.Tests.Shared;
using Servus.Akka;

namespace Njord.Tests.Pipeline;

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
                var mat = system.Materializer();
                var fakePipeline = system.ActorOf(
                    Props.Create(() => new FakePipelineActor(_offerProbe, mat)));
                registry.Register<PipelineActor>(fakePipeline);
            })
            .WithResolvableActors(r =>
            {
                r.Register<SchedulerActor>("scheduler");
            });
    }

    private IActorRef Scheduler => ActorRegistry.Get<SchedulerActor>();

    [Fact(Timeout = 5000)]
    public async Task Scheduler_offers_target_after_receiving_sink_ref()
    {
        var target = await _offerProbe.ExpectMsgAsync<WeightedTarget>();
        Assert.Equal("lucerne", target.Location.Name);
        Assert.Equal("icon_d2", target.Model.Id);
    }

    [Fact(Timeout = 5000)]
    public async Task Hash_change_triggers_ack_response()
    {
        await _offerProbe.ExpectMsgAsync<WeightedTarget>();

        var result = await Scheduler.Ask<Ack>(new HashResult("lucerne", "icon_d2", 42), TimeSpan.FromSeconds(2));
        Assert.NotNull(result);
    }

    [Fact(Timeout = 5000)]
    public async Task Unchanged_hash_also_acks()
    {
        await _offerProbe.ExpectMsgAsync<WeightedTarget>();

        await Scheduler.Ask<Ack>(new HashResult("lucerne", "icon_d2", 42), TimeSpan.FromSeconds(2));
        var result = await Scheduler.Ask<Ack>(new HashResult("lucerne", "icon_d2", 42), TimeSpan.FromSeconds(2));
        Assert.NotNull(result);
    }

    [Fact(Timeout = 5000)]
    public async Task Transport_failure_does_not_crash_and_allows_immediate_repoll()
    {
        await _offerProbe.ExpectMsgAsync<WeightedTarget>();

        Scheduler.Tell(new FetchFailed("lucerne", "icon_d2", FetchFailureReason.Transport, "test"));

        var result = await Scheduler.Ask<TriggerPollResult>(
            new TriggerImmediatePoll("lucerne", "icon_d2"), TimeSpan.FromSeconds(2));
        Assert.Equal(1, result.Count);
        await _offerProbe.ExpectMsgAsync<WeightedTarget>();
    }

    [Fact(Timeout = 5000)]
    public async Task Rate_limited_failure_does_not_crash_and_allows_immediate_repoll()
    {
        await _offerProbe.ExpectMsgAsync<WeightedTarget>();

        Scheduler.Tell(new FetchFailed("lucerne", "icon_d2", FetchFailureReason.RateLimited, "test"));

        var result = await Scheduler.Ask<TriggerPollResult>(
            new TriggerImmediatePoll("lucerne", "icon_d2"), TimeSpan.FromSeconds(2));
        Assert.Equal(1, result.Count);
    }

    [Fact(Timeout = 5000)]
    public async Task Model_unavailable_does_not_crash_and_allows_immediate_repoll()
    {
        await _offerProbe.ExpectMsgAsync<WeightedTarget>();

        Scheduler.Tell(new FetchFailed("lucerne", "icon_d2", FetchFailureReason.ModelUnavailable, "test"));

        var result = await Scheduler.Ask<TriggerPollResult>(
            new TriggerImmediatePoll("lucerne", "icon_d2"), TimeSpan.FromSeconds(2));
        Assert.Equal(1, result.Count);
    }

    [Fact(Timeout = 5000)]
    public async Task Malformed_payload_does_not_crash_and_allows_immediate_repoll()
    {
        await _offerProbe.ExpectMsgAsync<WeightedTarget>();

        Scheduler.Tell(new FetchFailed("lucerne", "icon_d2", FetchFailureReason.MalformedPayload, "test"));

        var result = await Scheduler.Ask<TriggerPollResult>(
            new TriggerImmediatePoll("lucerne", "icon_d2"), TimeSpan.FromSeconds(2));
        Assert.Equal(1, result.Count);
    }

    [Fact(Timeout = 5000)]
    public async Task Trigger_immediate_poll_for_all_returns_all_targets()
    {
        await _offerProbe.ExpectMsgAsync<WeightedTarget>();

        var result = await Scheduler.Ask<TriggerPollResult>(
            new TriggerImmediatePoll("", ""), TimeSpan.FromSeconds(2));

        Assert.Equal(1, result.Count);
        Assert.Contains("lucerne/icon_d2", result.Targets);
    }

    [Fact(Timeout = 5000)]
    public async Task Trigger_immediate_poll_for_unknown_location_returns_zero()
    {
        await _offerProbe.ExpectMsgAsync<WeightedTarget>();

        var result = await Scheduler.Ask<TriggerPollResult>(
            new TriggerImmediatePoll("nonexistent", ""), TimeSpan.FromSeconds(2));

        Assert.Equal(0, result.Count);
        Assert.Empty(result.Targets);
    }

    [Fact(Timeout = 5000)]
    public async Task Trigger_immediate_poll_actually_offers_target_to_pipeline()
    {
        await _offerProbe.ExpectMsgAsync<WeightedTarget>();

        await Scheduler.Ask<TriggerPollResult>(
            new TriggerImmediatePoll("lucerne", "icon_d2"), TimeSpan.FromSeconds(2));

        var latest = await _offerProbe.ExpectMsgAsync<WeightedTarget>();
        Assert.Equal("lucerne", latest.Location.Name);
        Assert.Equal("icon_d2", latest.Model.Id);
    }

    private sealed class FakePipelineActor : ReceiveActor
    {
        public FakePipelineActor(IActorRef probe, IMaterializer mat)
        {
            Receive<RequestPipelineSink>(_ =>
            {
                var (hubSink, hubSource) = MergeHub.Source<WeightedTarget>(perProducerBufferSize: 8)
                    .PreMaterialize(mat);

                hubSource
                    .RunWith(Sink.ForEach<WeightedTarget>(t => probe.Tell(t)), mat);

                StreamRefs.SinkRef<WeightedTarget>()
                    .To(hubSink)
                    .Run(mat)
                    .PipeTo(Sender, Self,
                        sr => new PipelineSinkResponse(sr),
                        _ => null!);
            });

            Receive<RequestPipelineSource>(_ =>
            {
                Source.Empty<FetchOutcome>()
                    .RunWith(StreamRefs.SourceRef<FetchOutcome>(), mat)
                    .PipeTo(Sender, Self,
                        sr => new PipelineSourceResponse(sr),
                        _ => null!);
            });
        }
    }
}
