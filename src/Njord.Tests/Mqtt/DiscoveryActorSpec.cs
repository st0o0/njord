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
using Njord.Tests.Shared;

namespace Njord.Tests.Mqtt;

public sealed class DiscoveryActorSpec : IDisposable
{
    private readonly ActorSystem _system = ActorSystem.Create("discovery-spec");

    public void Dispose() => _system.Dispose();

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

        return _system.ActorOf(Props.Create(() => new DiscoveryActor(
            Microsoft.Extensions.Options.Options.Create(options),
            parameters,
            features,
            NullLogger<DiscoveryActor>.Instance)));
    }

    private static ModelCapabilityLearned CreateCapability(
        string location = "lucerne",
        string modelId = "icon_d2",
        IReadOnlySet<ParameterDef>? supported = null)
    {
        supported ??= new HashSet<ParameterDef> { Temperature, WindSpeed };
        return new ModelCapabilityLearned(
            location,
            new WeatherModel(modelId),
            supported,
            [3, 6, 12, 24, 48],
            [0, 1]);
    }

    [Fact(Timeout = 5000)]
    public async Task Discovery_disabled_does_not_request_sink_or_subscribe()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();

        var fake = _system.ActorOf(Props.Create(() => new FakeMqttConnection(mat)));
        registry.Register<MqttConnectionActor>(fake, overwrite: true);

        var options = DefaultOptions(discoveryEnabled: false);
        CreateDiscoveryActor(options);

        await AsyncAssert.WaitUntil(async () =>
        {
            var received = await fake.Ask<List<object>>(new GetReceivedMessages(), TimeSpan.FromSeconds(1));
            return received.Count == 0;
        }, timeout: TimeSpan.FromMilliseconds(500));

        var finalReceived = await fake.Ask<List<object>>(new GetReceivedMessages(), TimeSpan.FromSeconds(2));
        Assert.Empty(finalReceived);
    }

    [Fact(Timeout = 5000)]
    public async Task Requests_sink_ref_and_subscribes_on_startup_when_enabled()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();

        var fake = _system.ActorOf(Props.Create(() => new FakeMqttConnection(mat)));
        registry.Register<MqttConnectionActor>(fake, overwrite: true);

        CreateDiscoveryActor();

        await AsyncAssert.WaitUntil(async () =>
        {
            var received = await fake.Ask<List<object>>(new GetReceivedMessages(), TimeSpan.FromSeconds(1));
            return received.Any(m => m is RequestMqttSink) && received.Any(m => m is SubscribeInbound);
        });

        var received = await fake.Ask<List<object>>(new GetReceivedMessages(), TimeSpan.FromSeconds(2));
        Assert.Contains(received, m => m is RequestMqttSink);
        Assert.Contains(received, m => m is SubscribeInbound);
    }

    [Fact(Timeout = 5000)]
    public async Task No_discovery_published_on_connect_before_capabilities_arrive()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();

        var probe = _system.ActorOf(Props.Create(() => new MqttMessageProbe(mat)));
        registry.Register<MqttConnectionActor>(probe, overwrite: true);

        var actor = CreateDiscoveryActor();

        await AsyncAssert.WaitUntil(async () =>
        {
            var received = await probe.Ask<List<object>>(new GetReceivedMessages(), TimeSpan.FromSeconds(1));
            return received.Any(m => m is RequestMqttSink);
        });

        actor.Tell(new MqttConnected());
        await Task.Delay(200);

        var messages = await probe.Ask<List<MqttMessage>>(new GetPublishedMessages(), TimeSpan.FromSeconds(1));
        Assert.Empty(messages);
    }

    [Fact(Timeout = 5000)]
    public async Task Publishes_discovery_after_all_capabilities_received()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();

        var probe = _system.ActorOf(Props.Create(() => new MqttMessageProbe(mat)));
        registry.Register<MqttConnectionActor>(probe, overwrite: true);

        var actor = CreateDiscoveryActor();

        await AsyncAssert.WaitUntil(async () =>
        {
            var received = await probe.Ask<List<object>>(new GetReceivedMessages(), TimeSpan.FromSeconds(1));
            return received.Any(m => m is RequestMqttSink);
        });

        actor.Tell(CreateCapability());

        await AsyncAssert.WaitUntil(async () =>
        {
            var messages = await probe.Ask<List<MqttMessage>>(new GetPublishedMessages(), TimeSpan.FromSeconds(1));
            return messages.Count > 0;
        });

        var messages = await probe.Ask<List<MqttMessage>>(new GetPublishedMessages(), TimeSpan.FromSeconds(2));
        Assert.NotEmpty(messages);
        Assert.All(messages, m => Assert.True(m.Retain));
    }

    [Fact(Timeout = 5000)]
    public async Task Timeout_triggers_partial_discovery()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();

        var probe = _system.ActorOf(Props.Create(() => new MqttMessageProbe(mat)));
        registry.Register<MqttConnectionActor>(probe, overwrite: true);

        var options = DefaultOptions();
        options.Models = ["icon_d2", "ecmwf_ifs025"];
        options.PollInterval = TimeSpan.FromMilliseconds(500);
        var actor = CreateDiscoveryActor(options);

        await AsyncAssert.WaitUntil(async () =>
        {
            var received = await probe.Ask<List<object>>(new GetReceivedMessages(), TimeSpan.FromSeconds(1));
            return received.Any(m => m is RequestMqttSink);
        });

        actor.Tell(CreateCapability(modelId: "icon_d2"));

        await AsyncAssert.WaitUntil(async () =>
        {
            var messages = await probe.Ask<List<MqttMessage>>(new GetPublishedMessages(), TimeSpan.FromSeconds(1));
            return messages.Count > 0;
        }, timeout: TimeSpan.FromSeconds(15));

        var messages = await probe.Ask<List<MqttMessage>>(new GetPublishedMessages(), TimeSpan.FromSeconds(2));
        Assert.NotEmpty(messages);
    }

    [Fact(Timeout = 5000)]
    public async Task Ha_birth_re_publishes_with_learned_state()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();

        var probe = _system.ActorOf(Props.Create(() => new MqttMessageProbe(mat)));
        registry.Register<MqttConnectionActor>(probe, overwrite: true);

        var actor = CreateDiscoveryActor();

        await AsyncAssert.WaitUntil(async () =>
        {
            var received = await probe.Ask<List<object>>(new GetReceivedMessages(), TimeSpan.FromSeconds(1));
            return received.Any(m => m is RequestMqttSink);
        });

        actor.Tell(CreateCapability());

        await AsyncAssert.WaitUntil(async () =>
        {
            var messages = await probe.Ask<List<MqttMessage>>(new GetPublishedMessages(), TimeSpan.FromSeconds(1));
            return messages.Count > 0;
        });

        var beforeBirth = (await probe.Ask<List<MqttMessage>>(new GetPublishedMessages(), TimeSpan.FromSeconds(1))).Count;

        actor.Tell(new MqttInboundMessage("homeassistant/status", "online"));

        await AsyncAssert.WaitUntil(async () =>
        {
            var messages = await probe.Ask<List<MqttMessage>>(new GetPublishedMessages(), TimeSpan.FromSeconds(1));
            return messages.Count > beforeBirth;
        });

        var afterBirth = await probe.Ask<List<MqttMessage>>(new GetPublishedMessages(), TimeSpan.FromSeconds(1));
        Assert.True(afterBirth.Count > beforeBirth);
    }

    [Fact(Timeout = 5000)]
    public async Task Late_capability_triggers_incremental_publish()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();

        var probe = _system.ActorOf(Props.Create(() => new MqttMessageProbe(mat)));
        registry.Register<MqttConnectionActor>(probe, overwrite: true);

        var options = DefaultOptions();
        options.Models = ["icon_d2", "ecmwf_ifs025"];
        var actor = CreateDiscoveryActor(options);

        await AsyncAssert.WaitUntil(async () =>
        {
            var received = await probe.Ask<List<object>>(new GetReceivedMessages(), TimeSpan.FromSeconds(1));
            return received.Any(m => m is RequestMqttSink);
        });

        actor.Tell(CreateCapability(modelId: "icon_d2"));
        actor.Tell(CreateCapability(modelId: "ecmwf_ifs025"));

        await AsyncAssert.WaitUntil(async () =>
        {
            var messages = await probe.Ask<List<MqttMessage>>(new GetPublishedMessages(), TimeSpan.FromSeconds(1));
            return messages.Count >= 2;
        });

        var messages = await probe.Ask<List<MqttMessage>>(new GetPublishedMessages(), TimeSpan.FromSeconds(1));
        Assert.True(messages.Count >= 2);
    }

    // -- query messages for fakes ------------------------------------------------

    private sealed record GetReceivedMessages;
    private sealed record GetPublishedMessages;

    // -- fake that records messages but does NOT respond with a SinkRef -----------

    private sealed class FakeMqttConnection : ReceiveActor
    {
        private readonly List<object> _received = [];

        public FakeMqttConnection(IMaterializer mat)
        {
            Receive<RequestMqttSink>(msg =>
            {
                _received.Add(msg);
                var sinkRef = StreamRefs.SinkRef<MqttMessage>()
                    .To(Sink.Ignore<MqttMessage>().MapMaterializedValue(_ => Akka.NotUsed.Instance))
                    .Run(mat);
                sinkRef.PipeTo(Sender, Self,
                    sr => new MqttSinkResponse(sr),
                    _ => null!);
            });

            Receive<SubscribeInbound>(msg => _received.Add(msg));

            Receive<GetReceivedMessages>(_ => Sender.Tell(new List<object>(_received)));
        }
    }

    // -- fake that captures published MqttMessages via a real Sink ----------------

    private sealed class MqttMessageProbe : ReceiveActor
    {
        private readonly List<MqttMessage> _published = [];
        private readonly List<object> _received = [];

        public MqttMessageProbe(IMaterializer mat)
        {
            Receive<RequestMqttSink>(msg =>
            {
                _received.Add(msg);
                var sink = Sink.ForEach<MqttMessage>(m => _published.Add(m))
                    .MapMaterializedValue(_ => Akka.NotUsed.Instance);
                var sinkRef = StreamRefs.SinkRef<MqttMessage>()
                    .To(sink)
                    .Run(mat);
                sinkRef.PipeTo(Sender, Self,
                    sr => new MqttSinkResponse(sr),
                    _ => null!);
            });

            Receive<SubscribeInbound>(msg => _received.Add(msg));

            Receive<GetReceivedMessages>(_ => Sender.Tell(new List<object>(_received)));
            Receive<GetPublishedMessages>(_ => Sender.Tell(new List<MqttMessage>(_published)));
        }
    }
}
