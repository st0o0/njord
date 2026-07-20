using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Logging.Abstractions;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Enrichment;
using Njord.Mqtt;

namespace Njord.Tests.Mqtt;

public sealed class DiscoveryActorSpec : Akka.Hosting.TestKit.TestKit
{
    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider) { }

    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
    private static readonly ParameterDef WindSpeed = ParameterRegistry.GetByApiName("wind_speed_10m")!;

    private static NjordOptions DefaultOptions(bool discoveryEnabled = true) => new()
    {
        Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
        Models = ["icon_d2"],
        Mqtt = new MqttOptions { DiscoveryEnabled = discoveryEnabled },
        PollInterval = TimeSpan.FromSeconds(5),
    };

    private IActorRef CreateDiscoveryActor(NjordOptions? options = null, EnrichmentOptions? enrichment = null)
    {
        options ??= DefaultOptions();
        enrichment ??= new EnrichmentOptions();
        var parameters = ParameterRegistry.Resolve(["Weather"], [], []);
        IEnumerable<IEnrichmentFeature> features = [];

        return Sys.ActorOf(Props.Create(() => new DiscoveryActor(
            Microsoft.Extensions.Options.Options.Create(options),
            parameters,
            features,
            NullLogger<DiscoveryActor>.Instance)));
    }

    private static EgressEvent.CapabilityLearned CreateCapability(
        string location = "lucerne",
        string modelId = "icon_d2",
        IReadOnlySet<ParameterDef>? supported = null)
    {
        supported ??= new HashSet<ParameterDef> { Temperature, WindSpeed };
        return new EgressEvent.CapabilityLearned(
            location,
            new WeatherModel(modelId),
            supported,
            [3, 6, 12, 24, 48],
            [0, 1]);
    }

    private FakeEgressHub RegisterFakeEgressHub()
    {
        var registry = ActorRegistry;
        var mat = Sys.Materializer();
        var hub = new FakeEgressHub(mat);
        registry.Register<EgressActor>(hub.Actor(Sys), overwrite: true);
        return hub;
    }

    [Fact(Timeout = 15000)]
    public async Task Discovery_disabled_does_not_request_sink_or_subscribe()
    {
        var registry = ActorRegistry;
        var mat = Sys.Materializer();
        var requestProbe = CreateTestProbe();

        var fake = Sys.ActorOf(Props.Create(() => new FakeMqttConnection(mat, requestProbe)));
        registry.Register<MqttConnectionActor>(fake, overwrite: true);
        RegisterFakeEgressHub();

        var options = DefaultOptions(discoveryEnabled: false);
        CreateDiscoveryActor(options);

        await requestProbe.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(500));
    }

    [Fact(Timeout = 15000)]
    public async Task Requests_sink_ref_and_subscribes_on_startup_when_enabled()
    {
        var registry = ActorRegistry;
        var mat = Sys.Materializer();
        var requestProbe = CreateTestProbe();

        var fake = Sys.ActorOf(Props.Create(() => new FakeMqttConnection(mat, requestProbe)));
        registry.Register<MqttConnectionActor>(fake, overwrite: true);
        RegisterFakeEgressHub();

        CreateDiscoveryActor();

        var msg1 = await requestProbe.ExpectMsgAsync<object>();
        var msg2 = await requestProbe.ExpectMsgAsync<object>();
        var received = new[] { msg1, msg2 };
        Assert.Contains(received, m => m is RequestMqttSink);
        Assert.Contains(received, m => m is SubscribeInbound);
    }

    [Fact(Timeout = 15000)]
    public async Task No_discovery_published_on_connect_before_capabilities_arrive()
    {
        var registry = ActorRegistry;
        var mat = Sys.Materializer();

        var requestProbe = CreateTestProbe();
        var publishProbe = CreateTestProbe();
        var mqttProbe = Sys.ActorOf(Props.Create(() => new MqttMessageProbe(mat, requestProbe, publishProbe)));
        registry.Register<MqttConnectionActor>(mqttProbe, overwrite: true);
        RegisterFakeEgressHub();

        var actor = CreateDiscoveryActor();

        await requestProbe.FishForMessageAsync(msg => msg is RequestMqttSink);

        actor.Tell(new MqttConnected());

        await publishProbe.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300));
    }

    [Fact(Timeout = 15000)]
    public async Task Publishes_discovery_after_all_capabilities_received()
    {
        var registry = ActorRegistry;
        var mat = Sys.Materializer();

        var requestProbe = CreateTestProbe();
        var publishProbe = CreateTestProbe();
        var mqttProbe = Sys.ActorOf(Props.Create(() => new MqttMessageProbe(mat, requestProbe, publishProbe)));
        registry.Register<MqttConnectionActor>(mqttProbe, overwrite: true);
        var hub = RegisterFakeEgressHub();

        CreateDiscoveryActor();

        await requestProbe.FishForMessageAsync(msg => msg is RequestMqttSink);
        await hub.WaitForQueue();

        hub.Emit(CreateCapability());

        var msg = await publishProbe.ExpectMsgAsync<MqttMessage>();
        Assert.True(msg.Retain);
    }

    [Fact(Timeout = 10000)]
    public async Task Timeout_triggers_partial_discovery()
    {
        var registry = ActorRegistry;
        var mat = Sys.Materializer();

        var requestProbe = CreateTestProbe();
        var publishProbe = CreateTestProbe();
        var mqttProbe = Sys.ActorOf(Props.Create(() => new MqttMessageProbe(mat, requestProbe, publishProbe)));
        registry.Register<MqttConnectionActor>(mqttProbe, overwrite: true);
        var hub = RegisterFakeEgressHub();

        var options = DefaultOptions();
        options.Models = ["icon_d2", "ecmwf_ifs025"];
        options.PollInterval = TimeSpan.FromMilliseconds(500);
        CreateDiscoveryActor(options);

        await requestProbe.FishForMessageAsync(msg => msg is RequestMqttSink);
        await hub.WaitForQueue();

        hub.Emit(CreateCapability(modelId: "icon_d2"));

        await publishProbe.ExpectMsgAsync<MqttMessage>();
    }

    [Fact(Timeout = 15000)]
    public async Task Ha_birth_re_publishes_with_learned_state()
    {
        var registry = ActorRegistry;
        var mat = Sys.Materializer();

        var requestProbe = CreateTestProbe();
        var publishProbe = CreateTestProbe();
        var mqttProbe = Sys.ActorOf(Props.Create(() => new MqttMessageProbe(mat, requestProbe, publishProbe)));
        registry.Register<MqttConnectionActor>(mqttProbe, overwrite: true);
        var hub = RegisterFakeEgressHub();

        var actor = CreateDiscoveryActor();

        await requestProbe.FishForMessageAsync(msg => msg is RequestMqttSink);
        await hub.WaitForQueue();

        hub.Emit(CreateCapability());

        // Wait for and drain the initial publish batch
        await publishProbe.ExpectMsgAsync<MqttMessage>();
        // Drain any additional messages from the initial publish batch
        await publishProbe.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(200));

        // Birth -> should re-publish
        actor.Tell(new MqttInboundMessage("homeassistant/status", "online"));
        await publishProbe.ExpectMsgAsync<MqttMessage>();
    }

    [Fact(Timeout = 15000)]
    public async Task Late_capability_triggers_incremental_publish()
    {
        var registry = ActorRegistry;
        var mat = Sys.Materializer();

        var requestProbe = CreateTestProbe();
        var publishProbe = CreateTestProbe();
        var mqttProbe = Sys.ActorOf(Props.Create(() => new MqttMessageProbe(mat, requestProbe, publishProbe)));
        registry.Register<MqttConnectionActor>(mqttProbe, overwrite: true);
        var hub = RegisterFakeEgressHub();

        var options = DefaultOptions();
        options.Models = ["icon_d2", "ecmwf_ifs025"];
        CreateDiscoveryActor(options);

        await requestProbe.FishForMessageAsync(msg => msg is RequestMqttSink);
        await hub.WaitForQueue();

        hub.Emit(CreateCapability(modelId: "icon_d2"));
        hub.Emit(CreateCapability(modelId: "ecmwf_ifs025"));

        await publishProbe.ExpectMsgAsync<MqttMessage>();
        await publishProbe.ExpectMsgAsync<MqttMessage>();
    }

    // -- fake egress hub that vends SourceRef and allows emitting events ----------

    private sealed class FakeEgressHub
    {
        private readonly IMaterializer _mat;
        private ISourceQueueWithComplete<EgressEvent>? _queue;
        private readonly TaskCompletionSource _queueReady = new();
        private IActorRef? _actor;

        public FakeEgressHub(IMaterializer mat)
        {
            _mat = mat;
        }

        public IActorRef Actor(ActorSystem system)
        {
            _actor = system.ActorOf(Props.Create(() => new FakeEgressSourceProvider(_mat, this)));
            return _actor;
        }

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

            Receive<RequestEgressSink>(_ =>
            {
                var sinkRef = StreamRefs.SinkRef<EgressEvent>()
                    .To(Sink.Ignore<EgressEvent>().MapMaterializedValue(_ => Akka.NotUsed.Instance))
                    .Run(mat);
                sinkRef.PipeTo(Sender, Self,
                    sr => new EgressSinkResponse(sr),
                    _ => null!);
            });
        }
    }

    // -- fake that records request messages via TestProbe -----------

    private sealed class FakeMqttConnection : ReceiveActor
    {
        public FakeMqttConnection(IMaterializer mat, IActorRef requestProbe)
        {
            Receive<RequestMqttSink>(msg =>
            {
                requestProbe.Tell(msg);
                var sinkRef = StreamRefs.SinkRef<MqttMessage>()
                    .To(Sink.Ignore<MqttMessage>().MapMaterializedValue(_ => Akka.NotUsed.Instance))
                    .Run(mat);
                sinkRef.PipeTo(Sender, Self,
                    sr => new MqttSinkResponse(sr),
                    _ => null!);
            });

            Receive<SubscribeInbound>(msg => requestProbe.Tell(msg));
        }
    }

    // -- fake that captures published MqttMessages via TestProbe ----

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

            Receive<SubscribeInbound>(msg => requestProbe.Tell(msg));
        }
    }
}
