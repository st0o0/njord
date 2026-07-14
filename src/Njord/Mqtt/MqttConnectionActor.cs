using Akka;
using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Egress;
using Njord.Mqtt.Transport;

namespace Njord.Mqtt;

public sealed record SubscribeInbound(IActorRef Listener);

public sealed record MqttConnected;

public sealed record MqttInboundMessage(string Topic, string Payload);

public sealed class MqttConnectionActor : ReceiveActor
{
    private readonly NjordOptions _options;
    private readonly IMqttConnection _connection;
    private readonly IMqttTransport _transport;
    private readonly ILogger<MqttConnectionActor> _logger;
    private readonly MqttEgressTuning _tuning;
    private readonly string _availabilityTopic;
    private readonly string _haStatusTopic;
    private int _connectAttempts;

    private ISourceQueueWithComplete<MqttMessage>? _availabilityQueue;
    private Sink<MqttMessage, NotUsed>? _mergeHubSink;
    private IMaterializer? _mat;

    private readonly List<IActorRef> _inboundListeners = [];

    private sealed record Connected;
    private sealed record ConnectFailed(Exception Cause);
    private sealed record Disconnected;
    private sealed record Reconnect;
    private sealed record Inbound(string Topic, string Payload);

    public MqttConnectionActor(
        IOptions<NjordOptions> options,
        IMqttConnection connection,
        IMqttTransport transport,
        ILogger<MqttConnectionActor> logger,
        MqttEgressTuning tuning)
    {
        _options = options.Value;
        _connection = connection;
        _transport = transport;
        _logger = logger;
        _tuning = tuning;
        _availabilityTopic = TopicScheme.AvailabilityTopic(_options.Mqtt.BaseTopic);
        _haStatusTopic = $"{_options.Mqtt.DiscoveryPrefix}/status";

        Ready();
    }

    protected override void PreStart()
    {
        _mat = Context.Materializer();
        MaterializeEgressGraph(_mat);
        Connect();
    }

    private void Ready()
    {
        ReceiveAsync<Connected>(OnConnectedAsync);
        Receive<ConnectFailed>(msg =>
        {
            _logger.LogWarning(msg.Cause, "MQTT connect to {Host}:{Port} failed",
                _options.Mqtt.Host, _options.Mqtt.Port);
            ScheduleReconnect();
        });
        Receive<Disconnected>(_ =>
        {
            _logger.LogWarning("MQTT connection lost — reconnecting");
            ScheduleReconnect();
        });
        Receive<Reconnect>(_ => Connect());
        Receive<Inbound>(OnInbound);
        Receive<RequestMqttSink>(_ =>
        {
            var sender = Sender;
            StreamRefs.SinkRef<MqttMessage>()
                .To(_mergeHubSink!)
                .Run(_mat!)
                .ContinueWith(t => t.IsCompletedSuccessfully
                    ? new MqttSinkResponse(t.Result)
                    : null)
                .PipeTo(sender);
        });
        Receive<SubscribeInbound>(msg =>
        {
            _inboundListeners.Add(msg.Listener);
            Context.Watch(msg.Listener);
        });
        Receive<Terminated>(msg =>
        {
            _inboundListeners.Remove(msg.ActorRef);
        });
    }

    protected override void PostStop()
    {
        _availabilityQueue?.OfferAsync(new MqttMessage(_availabilityTopic, "offline", true));
        _availabilityQueue?.Complete();
    }

    private void MaterializeEgressGraph(IMaterializer mat)
    {
        var (availQueue, availSource) = Source.Queue<MqttMessage>(8, OverflowStrategy.DropHead)
            .PreMaterialize(mat);
        _availabilityQueue = availQueue;

        var (hubSink, hubSource) = MergeHub.Source<MqttMessage>(perProducerBufferSize: 8)
            .PreMaterialize(mat);
        _mergeHubSink = hubSink;

        availSource.RunWith(hubSink, mat);

        hubSource
            .SelectAsync(1, async msg =>
            {
                await _transport.SendAsync(msg.Topic, msg.Payload, msg.Retain, CancellationToken.None);
                return NotUsed.Instance;
            })
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(
                _ => Akka.Streams.Supervision.Directive.Resume))
            .RunWith(Sink.Ignore<NotUsed>(), mat);
    }

    private void Connect()
    {
        var self = Self;
        _connection
            .ConnectAsync(
                (topic, payload) => self.Tell(new Inbound(topic, payload)),
                () => self.Tell(new Disconnected()),
                CancellationToken.None)
            .ContinueWith(t => t.IsCompletedSuccessfully
                ? (object)new Connected()
                : new ConnectFailed(t.Exception?.GetBaseException()
                    ?? new InvalidOperationException("connect canceled")))
            .PipeTo(self);
    }

    private void ScheduleReconnect()
    {
        _connectAttempts++;
        var factor = Math.Pow(2, Math.Min(_connectAttempts - 1, 6));
        var delay = TimeSpan.FromMilliseconds(_tuning.ReconnectDelay.TotalMilliseconds * factor);
        Context.System.Scheduler.ScheduleTellOnceCancelable(delay, Self, new Reconnect(), Self);
    }

    private async Task OnConnectedAsync(Connected _)
    {
        _connectAttempts = 0;

        try
        {
            await _connection.SubscribeAsync(_haStatusTopic, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Post-connect subscription failed");
        }

        _availabilityQueue?.OfferAsync(new MqttMessage(_availabilityTopic, "online", true));

        foreach (var listener in _inboundListeners)
        {
            listener.Tell(new MqttConnected());
        }
    }

    private void OnInbound(Inbound message)
    {
        var pub = new MqttInboundMessage(message.Topic, message.Payload);
        foreach (var listener in _inboundListeners)
        {
            listener.Tell(pub);
        }
    }
}
