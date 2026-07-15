using System.Collections.Concurrent;
using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Ingest;
using Njord.Mqtt;
using Njord.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Tests.Egress;

public sealed class ModelStateActorSpec : IDisposable
{
    private readonly ActorSystem _system = ActorSystem.Create("model-state-spec");

    public void Dispose() => _system.Dispose();

    private static readonly DateTimeOffset Anchor = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

    private static NjordOptions DefaultOptions() => new()
    {
        Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
        Models = ["icon_d2"],
    };

    private IActorRef CreateModelStateActor()
    {
        var options = DefaultOptions();
        var parameters = ParameterRegistry.Resolve(["Weather"], [], []);

        return _system.ActorOf(Props.Create(() => new ModelStateActor(
            Microsoft.Extensions.Options.Options.Create(options),
            parameters,
            NullLogger<ModelStateActor>.Instance)));
    }

    [Fact(Timeout = 5000)]
    public async Task Requests_egress_sink_and_pipeline_source_on_startup()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();
        var egressRequests = new ConcurrentBag<RequestEgressSink>();
        var pipelineRequests = new ConcurrentBag<RequestPipelineSource>();

        var fakeEgress = _system.ActorOf(FakeEgressSinkProvider.Props(mat, egressRequests));
        var fakePipeline = _system.ActorOf(FakePipelineSource.Props(mat, pipelineRequests));

        registry.Register<EgressActor>(fakeEgress, overwrite: true);
        registry.Register<PipelineActor>(fakePipeline, overwrite: true);
        registry.Register<DiscoveryActor>(_system.ActorOf(FakeDiscoveryActor.Props()), overwrite: true);

        CreateModelStateActor();

        await AsyncAssert.WaitUntil(() => egressRequests.Count >= 1 && pipelineRequests.Count >= 1);

        Assert.Single(egressRequests);
        Assert.Single(pipelineRequests);
    }

    [Fact(Timeout = 5000)]
    public async Task First_fetch_sends_capability_learned_to_discovery_actor()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();
        var capabilityMessages = new ConcurrentBag<ModelCapabilityLearned>();

        var fakeEgress = _system.ActorOf(FakeEgressSinkProvider.Props(mat));
        var fakeDiscovery = _system.ActorOf(FakeDiscoveryActor.Props(capabilityMessages));
        var forecast = CreateForecast("icon_d2", withNullParams: false);
        var fakePipeline = _system.ActorOf(FeedingPipelineSource.Props(mat, forecast));

        registry.Register<EgressActor>(fakeEgress, overwrite: true);
        registry.Register<PipelineActor>(fakePipeline, overwrite: true);
        registry.Register<DiscoveryActor>(fakeDiscovery, overwrite: true);

        CreateModelStateActor();

        await AsyncAssert.WaitUntil(() => capabilityMessages.Count >= 1);

        var msg = capabilityMessages.First();
        Assert.Equal("lucerne", msg.Location);
        Assert.Equal("icon_d2", msg.Model.Id);
        Assert.NotEmpty(msg.SupportedParameters);
        Assert.DoesNotContain(72, msg.ApplicableHorizons);
    }

    [Fact(Timeout = 5000)]
    public async Task Unchanged_capability_set_does_not_re_emit()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();
        var capabilityMessages = new ConcurrentBag<ModelCapabilityLearned>();
        var egressEvents = new ConcurrentBag<EgressEvent>();

        var fakeEgress = _system.ActorOf(FakeEgressSinkProvider.Props(mat, collectEvents: egressEvents));
        var fakeDiscovery = _system.ActorOf(FakeDiscoveryActor.Props(capabilityMessages));
        var forecast1 = CreateForecast("icon_d2", withNullParams: false);
        var forecast2 = CreateForecast("icon_d2", withNullParams: false, tempOffset: 1.0);
        var fakePipeline = _system.ActorOf(FeedingPipelineSource.Props(mat, forecast1, forecast2));

        registry.Register<EgressActor>(fakeEgress, overwrite: true);
        registry.Register<PipelineActor>(fakePipeline, overwrite: true);
        registry.Register<DiscoveryActor>(fakeDiscovery, overwrite: true);

        CreateModelStateActor();

        await AsyncAssert.WaitUntil(() => egressEvents.Count >= 2);

        Assert.Single(capabilityMessages);
    }

    [Fact(Timeout = 5000)]
    public async Task Expanded_capability_set_triggers_update()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();
        var capabilityMessages = new ConcurrentBag<ModelCapabilityLearned>();

        var fakeEgress = _system.ActorOf(FakeEgressSinkProvider.Props(mat));
        var fakeDiscovery = _system.ActorOf(FakeDiscoveryActor.Props(capabilityMessages));
        var forecast1 = CreateForecast("icon_d2", withNullParams: true);
        var forecast2 = CreateForecast("icon_d2", withNullParams: false);
        var fakePipeline = _system.ActorOf(FeedingPipelineSource.Props(mat, forecast1, forecast2));

        registry.Register<EgressActor>(fakeEgress, overwrite: true);
        registry.Register<PipelineActor>(fakePipeline, overwrite: true);
        registry.Register<DiscoveryActor>(fakeDiscovery, overwrite: true);

        CreateModelStateActor();

        await AsyncAssert.WaitUntil(() => capabilityMessages.Count >= 2);

        var messages = capabilityMessages.OrderBy(m => m.SupportedParameters.Count).ToList();
        Assert.True(messages[1].SupportedParameters.Count > messages[0].SupportedParameters.Count);
    }

    [Fact(Timeout = 5000)]
    public async Task Horizon_capping_excludes_72h_for_icon_d2()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();
        var capabilityMessages = new ConcurrentBag<ModelCapabilityLearned>();

        var fakeEgress = _system.ActorOf(FakeEgressSinkProvider.Props(mat));
        var fakeDiscovery = _system.ActorOf(FakeDiscoveryActor.Props(capabilityMessages));
        var forecast = CreateForecast("icon_d2", withNullParams: false);
        var fakePipeline = _system.ActorOf(FeedingPipelineSource.Props(mat, forecast));

        registry.Register<EgressActor>(fakeEgress, overwrite: true);
        registry.Register<PipelineActor>(fakePipeline, overwrite: true);
        registry.Register<DiscoveryActor>(fakeDiscovery, overwrite: true);

        CreateModelStateActor();

        await AsyncAssert.WaitUntil(() => capabilityMessages.Count >= 1);

        var msg = capabilityMessages.First();
        Assert.Contains(3, msg.ApplicableHorizons);
        Assert.Contains(24, msg.ApplicableHorizons);
        Assert.Contains(48, msg.ApplicableHorizons);
        Assert.DoesNotContain(72, msg.ApplicableHorizons);
        Assert.Equal(2, msg.ApplicableDayOffsets.Count);
    }

    private static ModelForecast CreateForecast(string modelId, bool withNullParams, double tempOffset = 0.0)
    {
        var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
        var wind = ParameterRegistry.GetByApiName("wind_speed_10m")!;

        var points = Enumerable.Range(0, 48)
            .Select(i => new ForecastPoint(
                Anchor.AddHours(i + 1),
                new Dictionary<ParameterDef, double?>
                {
                    [temp] = 20.0 + i + tempOffset,
                    [wind] = withNullParams ? null : 5.0 + i * 0.1,
                }))
            .ToList();

        return new ModelForecast(
            new WeatherModel(modelId), "lucerne", new CycleId(Anchor),
            new ForecastSeries(points), DailyForecastSeries.Empty);
    }

    private sealed class FakeEgressSinkProvider : ReceiveActor
    {
        public FakeEgressSinkProvider(
            IMaterializer mat,
            ConcurrentBag<RequestEgressSink>? requests = null,
            ConcurrentBag<EgressEvent>? collectEvents = null)
        {
            Receive<RequestEgressSink>(msg =>
            {
                requests?.Add(msg);

                Sink<EgressEvent, Akka.NotUsed> sink = collectEvents is not null
                    ? Sink.ForEach<EgressEvent>(e => collectEvents.Add(e)).MapMaterializedValue(_ => Akka.NotUsed.Instance)
                    : Sink.Ignore<EgressEvent>().MapMaterializedValue(_ => Akka.NotUsed.Instance);

                var sinkRef = StreamRefs.SinkRef<EgressEvent>()
                    .To(sink)
                    .Run(mat);
                sinkRef.PipeTo(Sender, Self,
                    sr => new EgressSinkResponse(sr),
                    _ => null!);
            });
        }

        public static Props Props(
            IMaterializer mat,
            ConcurrentBag<RequestEgressSink>? requests = null,
            ConcurrentBag<EgressEvent>? collectEvents = null) =>
            Akka.Actor.Props.Create(() => new FakeEgressSinkProvider(mat, requests, collectEvents));
    }

    private sealed class FakePipelineSource : ReceiveActor
    {
        public FakePipelineSource(IMaterializer mat, ConcurrentBag<RequestPipelineSource>? requests = null)
        {
            Receive<RequestPipelineSource>(msg =>
            {
                requests?.Add(msg);
                var sourceRef = Source.Empty<FetchOutcome>()
                    .RunWith(StreamRefs.SourceRef<FetchOutcome>(), mat)
                    .Result;
                Sender.Tell(new PipelineSourceResponse(sourceRef));
            });
        }

        public static Props Props(IMaterializer mat, ConcurrentBag<RequestPipelineSource>? requests = null) =>
            Akka.Actor.Props.Create(() => new FakePipelineSource(mat, requests));
    }

    private sealed class FeedingPipelineSource : ReceiveActor
    {
        public FeedingPipelineSource(IMaterializer mat, params ModelForecast[] forecasts)
        {
            Receive<RequestPipelineSource>(_ =>
            {
                var outcomes = forecasts.Select(f => (FetchOutcome)new FetchOutcome.Success(f));
                var sourceRef = Source.From(outcomes)
                    .RunWith(StreamRefs.SourceRef<FetchOutcome>(), mat)
                    .Result;
                Sender.Tell(new PipelineSourceResponse(sourceRef));
            });
        }

        public static Props Props(IMaterializer mat, params ModelForecast[] forecasts) =>
            Akka.Actor.Props.Create(() => new FeedingPipelineSource(mat, forecasts));
    }

    private sealed class FakeDiscoveryActor : ReceiveActor
    {
        public FakeDiscoveryActor(ConcurrentBag<ModelCapabilityLearned>? messages = null)
        {
            Receive<ModelCapabilityLearned>(msg => messages?.Add(msg));
        }

        public static Props Props(ConcurrentBag<ModelCapabilityLearned>? messages = null) =>
            Akka.Actor.Props.Create(() => new FakeDiscoveryActor(messages));
    }
}
