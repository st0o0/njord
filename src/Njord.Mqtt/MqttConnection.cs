using Akka.Actor;
using Akka.Event;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration.Mqtt;
using Njord.Core.Health;
using Njord.Messages.Mqtt;
using Njord.Mqtt.Transport;

namespace Njord.Mqtt;

public sealed class MqttConnection : ReceiveActor
{
    private sealed record Connected;
    private sealed record ConnectFailed(Exception Cause);
    private sealed record Disconnected;
    private sealed record Reconnect;
    private sealed record Inbound(string Topic, string Payload);

    private readonly MqttOptions _mqttOptions;
    private readonly IMqttConnection _connection;
    private readonly IMqttTransport _transport;
    private readonly MqttEgressTuning _tuning;
    private readonly NjordHealthMonitor _healthState;
    private readonly TimeProvider _timeProvider;

    private readonly string _availabilityTopic;
    private readonly string _haStatusTopic;
    private int _connectAttempts;
    private readonly List<IActorRef> _inboundListeners = [];
    private readonly ILoggingAdapter _log = Context.GetLogger();

    public MqttConnection(
        IOptions<MqttOptions> mqttOptions,
        IMqttConnection connection,
        IMqttTransport transport,
        MqttEgressTuning tuning,
        NjordHealthMonitor healthState,
        TimeProvider timeProvider)
    {
        _mqttOptions = mqttOptions.Value;
        _connection = connection;
        _transport = transport;
        _tuning = tuning;
        _healthState = healthState;
        _timeProvider = timeProvider;
        _availabilityTopic = TopicScheme.AvailabilityTopic(_mqttOptions.BaseTopic);
        _haStatusTopic = $"{_mqttOptions.DiscoveryPrefix}/status";

        Ready();
    }

    protected override void PreStart()
    {
        Connect();
    }

    private void Ready()
    {
        ReceiveAsync<Connected>(OnConnectedAsync);
        Receive<ConnectFailed>(msg =>
        {
            _log.Warning(msg.Cause, "MQTT connect to {Host}:{Port} failed",
                _mqttOptions.Host, _mqttOptions.Port);
            ScheduleReconnect();
        });
        Receive<Disconnected>(_ =>
        {
            _healthState.UpdateMqtt(new MqttHealthSnapshot(false, _timeProvider.GetUtcNow()));
            Telemetry.MqttConnected.Record(0);
            _log.Warning("MQTT connection lost — reconnecting");
            ScheduleReconnect();
        });
        Receive<Reconnect>(_ => Connect());
        Receive<Inbound>(OnInbound);
        Receive<SubscribeInbound>(_ =>
        {
            _inboundListeners.Add(Sender);
            Context.Watch(Sender);
        });
        Receive<Terminated>(msg =>
        {
            _inboundListeners.Remove(msg.ActorRef);
        });
    }

    protected override void PostStop()
    {
        _transport.SendAsync(_availabilityTopic, "offline", true, CancellationToken.None);
    }

    private void Connect()
    {
        var self = Self;
        _connection
            .ConnectAsync(
                (topic, payload) => self.Tell(new Inbound(topic, payload)),
                () => self.Tell(new Disconnected()),
                CancellationToken.None)
            .PipeTo(self,
                success: () => new Connected(),
                failure: ex => new ConnectFailed(ex));
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
        _log.Info("MQTT connected to {Host}:{Port}", _mqttOptions.Host, _mqttOptions.Port);
        _healthState.UpdateMqtt(new MqttHealthSnapshot(true, null));
        Telemetry.MqttConnected.Record(1);

        try
        {
            await _connection.SubscribeAsync(_haStatusTopic, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Post-connect subscription failed");
        }

        await _transport.SendAsync(_availabilityTopic, "online", true, CancellationToken.None);

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
