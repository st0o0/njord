using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Logging.Abstractions;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Mqtt;

namespace Njord.Tests.Mqtt;

public sealed class DiscoveryActorSpec : IDisposable
{
    private readonly ActorSystem _system = ActorSystem.Create("discovery-spec");

    public void Dispose() => _system.Dispose();

    private static NjordOptions DefaultOptions(bool discoveryEnabled = true) => new()
    {
        Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
        Models = ["icon_d2"],
        Mqtt = new MqttOptions { DiscoveryEnabled = discoveryEnabled },
    };

    private IActorRef CreateDiscoveryActor(NjordOptions? options = null, EnrichmentOptions? enrichment = null)
    {
        options ??= DefaultOptions();
        enrichment ??= new EnrichmentOptions();
        var parameters = ParameterRegistry.Resolve(["Weather"], [], []);

        return _system.ActorOf(Props.Create(() => new DiscoveryActor(
            Microsoft.Extensions.Options.Options.Create(options),
            Microsoft.Extensions.Options.Options.Create(enrichment),
            parameters,
            NullLogger<DiscoveryActor>.Instance)));
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

        await Task.Delay(500);

        var received = await fake.Ask<List<object>>(new GetReceivedMessages(), TimeSpan.FromSeconds(2));
        Assert.Empty(received);
    }

    [Fact(Timeout = 5000)]
    public async Task Requests_sink_ref_and_subscribes_on_startup_when_enabled()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();

        var fake = _system.ActorOf(Props.Create(() => new FakeMqttConnection(mat)));
        registry.Register<MqttConnectionActor>(fake, overwrite: true);

        CreateDiscoveryActor();

        await Task.Delay(500);

        var received = await fake.Ask<List<object>>(new GetReceivedMessages(), TimeSpan.FromSeconds(2));
        Assert.Contains(received, m => m is RequestMqttSink);
        Assert.Contains(received, m => m is SubscribeInbound);
    }

    [Fact(Timeout = 5000)]
    public async Task Publishes_discovery_on_mqtt_connected()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();

        var probe = _system.ActorOf(Props.Create(() => new MqttMessageProbe(mat)));
        registry.Register<MqttConnectionActor>(probe, overwrite: true);

        var actor = CreateDiscoveryActor();

        // Wait for SinkRef handshake to complete
        await Task.Delay(500);

        // Tell the actor the broker is connected — triggers PublishDiscovery
        actor.Tell(new MqttConnected());

        await Task.Delay(500);

        var messages = await probe.Ask<List<MqttMessage>>(new GetPublishedMessages(), TimeSpan.FromSeconds(2));
        Assert.NotEmpty(messages);
        Assert.All(messages, m => Assert.True(m.Retain));
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
