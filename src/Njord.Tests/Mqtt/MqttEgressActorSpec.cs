using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Time.Testing;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Enrichment;
using Njord.Mqtt;

namespace Njord.Tests.Mqtt;

public sealed class MqttEgressActorSpec : Akka.Hosting.TestKit.TestKit
{
    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider) { }

    private static readonly DateTimeOffset Anchor = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

    private static NjordOptions DefaultOptions() => new()
    {
        Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
        Models = ["icon_d2"],
        Mqtt = new MqttOptions { BaseTopic = "njord" },
    };

    private IActorRef CreateMqttEgressActor(NjordOptions? options = null, TimeProvider? timeProvider = null)
    {
        options ??= DefaultOptions();
        timeProvider ??= new FakeTimeProvider(Anchor);
        var parameters = ParameterRegistry.Resolve(["Weather"], [], []);
        IEnumerable<IEnrichmentFeature> features = [];

        return Sys.ActorOf(Props.Create(() => new MqttEgressActor(
            Microsoft.Extensions.Options.Options.Create(options),
            parameters,
            timeProvider,
            features)));
    }

    private FakeEgressHub RegisterFakeEgressHub()
    {
        var mat = Sys.Materializer();
        var hub = new FakeEgressHub(mat);
        ActorRegistry.Register<EgressActor>(hub.Actor(Sys), overwrite: true);
        return hub;
    }

    private (IActorRef Probe, Akka.TestKit.TestProbe RequestProbe, Akka.TestKit.TestProbe PublishProbe)
        RegisterFakeMqttConnection()
    {
        var mat = Sys.Materializer();
        var requestProbe = CreateTestProbe();
        var publishProbe = CreateTestProbe();
        var probe = Sys.ActorOf(Props.Create(() =>
            new MqttMessageProbe(mat, requestProbe, publishProbe)));
        ActorRegistry.Register<MqttConnectionActor>(probe, overwrite: true);
        return (probe, requestProbe, publishProbe);
    }

    /// <summary>
    /// Wait for both refs to be requested and give stream ref PipeTo responses
    /// time to propagate back to MqttEgressActor and materialize the graph.
    /// </summary>
    private async Task WaitForGraphMaterialized(
        Akka.TestKit.TestProbe requestProbe, FakeEgressHub hub)
    {
        await requestProbe.ExpectMsgAsync<RequestMqttSink>();
        await hub.WaitForQueue();
        // Stream ref materialization (PipeTo) + graph wiring is async;
        // allow the actor to process both responses and call MaterializeGraph.
        await Task.Delay(500);
    }

    [Fact(Timeout = 15000)]
    public async Task Should_request_both_egress_source_and_mqtt_sink_on_startup()
    {
        var hub = RegisterFakeEgressHub();
        var (_, requestProbe, publishProbe) = RegisterFakeMqttConnection();

        CreateMqttEgressActor();

        // Actor must request the MqttSink from MqttConnectionActor
        await requestProbe.ExpectMsgAsync<RequestMqttSink>();
        // Actor must request the EgressSource from EgressActor (confirmed by queue creation)
        await hub.WaitForQueue();

        // Verify the graph is live by emitting an event and expecting output
        await Task.Delay(500);
        var forecast = CreateForecast("icon_d2");
        hub.Emit(new EgressEvent.PerModelUpdate("lucerne", new WeatherModel("icon_d2"), forecast));

        var msg = await publishProbe.ExpectMsgAsync<MqttMessage>(TimeSpan.FromSeconds(5));
        Assert.NotNull(msg);
    }

    [Fact(Timeout = 15000)]
    public async Task Should_publish_per_model_update_as_mqtt_messages()
    {
        var hub = RegisterFakeEgressHub();
        var (_, requestProbe, publishProbe) = RegisterFakeMqttConnection();

        CreateMqttEgressActor();

        await WaitForGraphMaterialized(requestProbe, hub);

        var forecast = CreateForecast("icon_d2");
        hub.Emit(new EgressEvent.PerModelUpdate("lucerne", new WeatherModel("icon_d2"), forecast));

        var msg = await publishProbe.ExpectMsgAsync<MqttMessage>(TimeSpan.FromSeconds(5));
        Assert.StartsWith("njord/", msg.Topic);
        Assert.True(msg.Retain);
        Assert.NotEmpty(msg.Payload);
    }

    [Fact(Timeout = 15000)]
    public async Task Should_re_request_refs_after_watched_actor_terminates()
    {
        var mat = Sys.Materializer();
        var hub = RegisterFakeEgressHub();

        var requestProbe = CreateTestProbe();
        var publishProbe = CreateTestProbe();
        var fakeMqtt = Sys.ActorOf(Props.Create(() =>
            new MqttMessageProbe(mat, requestProbe, publishProbe)));
        ActorRegistry.Register<MqttConnectionActor>(fakeMqtt, overwrite: true);

        CreateMqttEgressActor();

        // Wait for initial ref request
        await requestProbe.ExpectMsgAsync<RequestMqttSink>();

        // Terminate the fake MqttConnectionActor
        await fakeMqtt.GracefulStop(TimeSpan.FromSeconds(2));
        await Task.Delay(200);

        // Register a replacement so GetActorAsync resolves again
        var newRequestProbe = CreateTestProbe();
        var newPublishProbe = CreateTestProbe();
        var newFakeMqtt = Sys.ActorOf(Props.Create(() =>
            new MqttMessageProbe(mat, newRequestProbe, newPublishProbe)));
        ActorRegistry.Register<MqttConnectionActor>(newFakeMqtt, overwrite: true);

        // The actor should re-request refs after Terminated
        var reRequest = await newRequestProbe.FishForMessageAsync(
            msg => msg is RequestMqttSink, TimeSpan.FromSeconds(5));
        Assert.IsType<RequestMqttSink>(reRequest);
    }

    private static ModelForecast CreateForecast(string modelId)
    {
        var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
        var wind = ParameterRegistry.GetByApiName("wind_speed_10m")!;

        var points = Enumerable.Range(0, 60)
            .Select(i => new ForecastPoint(
                Anchor.AddHours(i + 1),
                new Dictionary<ParameterDef, double?>
                {
                    [temp] = 20.0 + i,
                    [wind] = 5.0 + i * 0.1,
                }))
            .ToList();

        return new ModelForecast(
            new WeatherModel(modelId), "lucerne", new CycleId(Anchor),
            new ForecastSeries(points), DailyForecastSeries.Empty);
    }

    // -- fakes ---------------------------------------------------------------

    private sealed class FakeEgressHub
    {
        private readonly IMaterializer _mat;
        private ISourceQueueWithComplete<EgressEvent>? _queue;
        private readonly TaskCompletionSource _queueReady = new();

        public FakeEgressHub(IMaterializer mat) => _mat = mat;

        public IActorRef Actor(ActorSystem system)
            => system.ActorOf(Props.Create(() => new FakeEgressSourceProvider(_mat, this)));

        public void Emit(EgressEvent evt) => _queue?.OfferAsync(evt);

        public Task WaitForQueue() => _queueReady.Task;

        internal void SetQueue(ISourceQueueWithComplete<EgressEvent> queue)
        {
            _queue = queue;
            _queueReady.TrySetResult();
        }
    }

    private sealed class FakeEgressSourceProvider : ReceiveActor
    {
        public FakeEgressSourceProvider(IMaterializer mat, FakeEgressHub hub)
        {
            Receive<RequestEgressSource>(_ =>
            {
                var (queue, source) = Source.Queue<EgressEvent>(32, OverflowStrategy.DropHead)
                    .PreMaterialize(mat);
                hub.SetQueue(queue);

                source
                    .RunWith(StreamRefs.SourceRef<EgressEvent>(), mat)
                    .PipeTo(Sender, Self,
                        sr => new EgressSourceResponse(sr),
                        _ => null!);
            });
        }
    }

    private sealed class MqttMessageProbe : ReceiveActor
    {
        public MqttMessageProbe(IMaterializer mat, IActorRef requestProbe, IActorRef publishProbe)
        {
            Receive<RequestMqttSink>(msg =>
            {
                requestProbe.Tell(msg);
                var sink = Sink.ForEach<MqttMessage>(m => publishProbe.Tell(m))
                    .MapMaterializedValue(_ => Akka.NotUsed.Instance);
                var sinkRef = StreamRefs.SinkRef<MqttMessage>()
                    .To(sink)
                    .Run(mat);
                sinkRef.PipeTo(Sender, Self,
                    sr => new MqttSinkResponse(sr),
                    _ => null!);
            });
        }
    }
}
