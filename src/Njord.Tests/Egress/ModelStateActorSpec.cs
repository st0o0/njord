using System.Collections.Concurrent;
using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Logging.Abstractions;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Ingest;
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

        CreateModelStateActor();

        await AsyncAssert.WaitUntil(() => egressRequests.Count >= 1 && pipelineRequests.Count >= 1);

        Assert.Single(egressRequests);
        Assert.Single(pipelineRequests);
    }

    [Fact(Timeout = 5000)]
    public async Task First_fetch_emits_capability_learned_into_egress_sink()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();
        var egressEvents = new ConcurrentBag<EgressEvent>();

        var fakeEgress = _system.ActorOf(FakeEgressSinkProvider.Props(mat, collectEvents: egressEvents));
        var forecast = CreateForecast("icon_d2", withNullParams: false);
        var fakePipeline = _system.ActorOf(FeedingPipelineSource.Props(mat, forecast));

        registry.Register<EgressActor>(fakeEgress, overwrite: true);
        registry.Register<PipelineActor>(fakePipeline, overwrite: true);

        CreateModelStateActor();

        await AsyncAssert.WaitUntil(() => egressEvents.Count >= 2);

        var capEvent = egressEvents.OfType<EgressEvent.CapabilityLearned>().Single();
        Assert.Equal("lucerne", capEvent.Location);
        Assert.Equal("icon_d2", capEvent.Model.Id);
        Assert.NotEmpty(capEvent.SupportedParameters);
        Assert.DoesNotContain(72, capEvent.ApplicableHorizons);

        Assert.Single(egressEvents.OfType<EgressEvent.PerModelUpdate>());
    }

    [Fact(Timeout = 5000)]
    public async Task Unchanged_capability_set_does_not_re_emit()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();
        var egressEvents = new ConcurrentBag<EgressEvent>();

        var fakeEgress = _system.ActorOf(FakeEgressSinkProvider.Props(mat, collectEvents: egressEvents));
        var forecast1 = CreateForecast("icon_d2", withNullParams: false);
        var forecast2 = CreateForecast("icon_d2", withNullParams: false, tempOffset: 1.0);
        var fakePipeline = _system.ActorOf(FeedingPipelineSource.Props(mat, forecast1, forecast2));

        registry.Register<EgressActor>(fakeEgress, overwrite: true);
        registry.Register<PipelineActor>(fakePipeline, overwrite: true);

        CreateModelStateActor();

        await AsyncAssert.WaitUntil(() => egressEvents.OfType<EgressEvent.PerModelUpdate>().Count() >= 2);

        Assert.Single(egressEvents.OfType<EgressEvent.CapabilityLearned>());
    }

    [Fact(Timeout = 5000)]
    public async Task Expanded_capability_set_triggers_update()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();
        var egressEvents = new ConcurrentBag<EgressEvent>();

        var fakeEgress = _system.ActorOf(FakeEgressSinkProvider.Props(mat, collectEvents: egressEvents));
        var forecast1 = CreateForecast("icon_d2", withNullParams: true);
        var forecast2 = CreateForecast("icon_d2", withNullParams: false);
        var fakePipeline = _system.ActorOf(FeedingPipelineSource.Props(mat, forecast1, forecast2));

        registry.Register<EgressActor>(fakeEgress, overwrite: true);
        registry.Register<PipelineActor>(fakePipeline, overwrite: true);

        CreateModelStateActor();

        await AsyncAssert.WaitUntil(() => egressEvents.OfType<EgressEvent.CapabilityLearned>().Count() >= 2);

        var capEvents = egressEvents.OfType<EgressEvent.CapabilityLearned>()
            .OrderBy(m => m.SupportedParameters.Count).ToList();
        Assert.True(capEvents[1].SupportedParameters.Count > capEvents[0].SupportedParameters.Count);
    }

    [Fact(Timeout = 5000)]
    public async Task Horizon_capping_excludes_72h_for_icon_d2()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();
        var egressEvents = new ConcurrentBag<EgressEvent>();

        var fakeEgress = _system.ActorOf(FakeEgressSinkProvider.Props(mat, collectEvents: egressEvents));
        var forecast = CreateForecast("icon_d2", withNullParams: false);
        var fakePipeline = _system.ActorOf(FeedingPipelineSource.Props(mat, forecast));

        registry.Register<EgressActor>(fakeEgress, overwrite: true);
        registry.Register<PipelineActor>(fakePipeline, overwrite: true);

        CreateModelStateActor();

        await AsyncAssert.WaitUntil(() => egressEvents.OfType<EgressEvent.CapabilityLearned>().Any());

        var cap = egressEvents.OfType<EgressEvent.CapabilityLearned>().First();
        Assert.Contains(3, cap.ApplicableHorizons);
        Assert.Contains(24, cap.ApplicableHorizons);
        Assert.Contains(48, cap.ApplicableHorizons);
        Assert.DoesNotContain(72, cap.ApplicableHorizons);
        Assert.Equal(3, cap.ApplicableDayOffsets.Count);
    }

    private static ModelForecast CreateForecast(string modelId, bool withNullParams, double tempOffset = 0.0)
    {
        var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
        var wind = ParameterRegistry.GetByApiName("wind_speed_10m")!;

        var points = Enumerable.Range(0, 60)
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
}
