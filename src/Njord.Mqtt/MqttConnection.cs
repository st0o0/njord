using Akka.Actor;
using Akka.Event;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration.Mqtt;
using Njord.Core.Health;
using Njord.Messages.Mqtt;
using Njord.Mqtt.Transport;

namespace Njord.Mqtt;

public sealed class MqttConnection : ReceiveActor, IWithTimers, IWithUnboundedStash
{
    private const string ReconnectTimer = "reconnect";
    private const string OnlineRetryTimer = "online-retry";
    private const int MaxBackoffExponent = 6;

    private sealed record Connected;
    private sealed record ConnectFailed(Exception Cause);
    private sealed record Disconnected;
    private sealed record Reconnect;
    private sealed record Inbound(string Topic, string Payload);
    private sealed record StatusSubscribed(int Session);
    private sealed record StatusSubscribeFailed(int Session, Exception Cause);
    private sealed record PublishOnline(int Session);
    private sealed record OnlinePublished(int Session);
    private sealed record OnlinePublishFailed(int Session, Exception Cause);

    private readonly MqttOptions _mqttOptions;
    private readonly IMqttConnection _connection;
    private readonly IMqttTransport _transport;
    private readonly MqttEgressTuning _tuning;
    private readonly NjordHealthMonitor _healthState;
    private readonly TimeProvider _timeProvider;

    private readonly string _availabilityTopic;
    private readonly string _haStatusTopic;
    private readonly List<IActorRef> _inboundListeners = [];
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private int _connectAttempts;
    private int _onlineAttempts;
    private int _session;

    public ITimerScheduler Timers { get; set; } = null!;

    public IStash Stash { get; set; } = null!;

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

        Connecting();
    }

    protected override void PreStart()
    {
        Connect();
    }

    protected override void PostStop()
    {
        _ = PublishOfflineAsync(_transport, _availabilityTopic, _tuning.PublishTimeout, _log);
    }

    private void Connecting()
    {
        Receive<Connected>(_ =>
        {
            Become(ConnectedState);
            OnConnectionEstablished();
            Stash.UnstashAll();
        });
        Receive<ConnectFailed>(msg =>
        {
            _log.Warning(msg.Cause, "MQTT connect to {Host}:{Port} failed",
                _mqttOptions.Host, _mqttOptions.Port);
            Become(WaitingToReconnect);
            ScheduleReconnect();
            Stash.UnstashAll();
        });
        Receive<Disconnected>(_ => Stash.Stash());
        ReceiveCommon();
    }

    private void WaitingToReconnect()
    {
        Receive<Reconnect>(_ =>
        {
            Become(Connecting);
            Connect();
        });
        Receive<Disconnected>(_ => _log.Debug("Ignoring MQTT disconnect signal while not connected"));
        ReceiveCommon();
    }

    private void ConnectedState()
    {
        Receive<Disconnected>(_ =>
        {
            _healthState.UpdateMqtt(new MqttHealthSnapshot(false, _timeProvider.GetUtcNow()));
            Telemetry.MqttConnected.Record(0);
            _log.Warning("MQTT connection lost — reconnecting");
            Timers.Cancel(OnlineRetryTimer);
            Become(WaitingToReconnect);
            ScheduleReconnect();
        });
        Receive<StatusSubscribed>(msg => msg.Session == _session,
            _ => _log.Debug("Subscribed to {Topic}", _haStatusTopic));
        Receive<StatusSubscribeFailed>(msg => msg.Session == _session,
            msg => _log.Warning(msg.Cause,
                "Subscribing to {Topic} failed - it is subscribed again with the next connect", _haStatusTopic));
        Receive<PublishOnline>(msg => msg.Session == _session, _ => PublishOnlineStatus());
        Receive<OnlinePublished>(msg => msg.Session == _session, _ =>
        {
            _onlineAttempts = 0;
            _log.Debug("Published online on {Topic}", _availabilityTopic);
        });
        Receive<OnlinePublishFailed>(msg => msg.Session == _session, msg =>
        {
            _onlineAttempts++;
            var delay = Backoff(_onlineAttempts);
            _log.Warning(msg.Cause, "Publishing online on {Topic} failed - retrying in {Delay}",
                _availabilityTopic, delay);
            Timers.StartSingleTimer(OnlineRetryTimer, new PublishOnline(_session), delay);
        });
        ReceiveCommon();
    }

    private void ReceiveCommon()
    {
        Receive<Inbound>(OnInbound);
        Receive<SubscribeInbound>(_ =>
        {
            _inboundListeners.Add(Sender);
            Context.Watch(Sender);
        });
        Receive<Terminated>(msg => _inboundListeners.Remove(msg.ActorRef));
        Receive<StatusSubscribed>(_ => { });
        Receive<StatusSubscribeFailed>(msg =>
            _log.Debug("Ignoring subscribe failure of a previous MQTT connection: {Message}", msg.Cause.Message));
        Receive<PublishOnline>(_ => { });
        Receive<OnlinePublished>(_ => { });
        Receive<OnlinePublishFailed>(msg =>
            _log.Debug("Ignoring online publish failure of a previous MQTT connection: {Message}", msg.Cause.Message));
        Receive<Reconnect>(_ => _log.Debug("Ignoring reconnect while a connection is in flight or established"));
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
        Timers.StartSingleTimer(ReconnectTimer, new Reconnect(), Backoff(_connectAttempts));
    }

    private TimeSpan Backoff(int attempt)
    {
        var factor = Math.Pow(2, Math.Min(attempt - 1, MaxBackoffExponent));
        return TimeSpan.FromMilliseconds(_tuning.ReconnectDelay.TotalMilliseconds * factor);
    }

    private void OnConnectionEstablished()
    {
        _session++;
        _connectAttempts = 0;
        _onlineAttempts = 0;
        _log.Info("MQTT connected to {Host}:{Port}", _mqttOptions.Host, _mqttOptions.Port);
        _healthState.UpdateMqtt(new MqttHealthSnapshot(true, null));
        Telemetry.MqttConnected.Record(1);

        foreach (var listener in _inboundListeners)
        {
            listener.Tell(new MqttConnected());
        }

        var session = _session;
        WithTimeout(ct => _connection.SubscribeAsync(_haStatusTopic, ct), _tuning.PublishTimeout)
            .PipeTo(Self,
                success: () => new StatusSubscribed(session),
                failure: ex => new StatusSubscribeFailed(session, ex));
        PublishOnlineStatus();
    }

    private void PublishOnlineStatus()
    {
        var session = _session;
        var transport = _transport;
        var topic = _availabilityTopic;
        WithTimeout(ct => transport.SendAsync(topic, "online", true, ct), _tuning.PublishTimeout)
            .PipeTo(Self,
                success: () => new OnlinePublished(session),
                failure: ex => new OnlinePublishFailed(session, ex));
    }

    private void OnInbound(Inbound message)
    {
        var pub = new MqttInboundMessage(message.Topic, message.Payload);
        foreach (var listener in _inboundListeners)
        {
            listener.Tell(pub);
        }
    }

    private static async Task WithTimeout(Func<CancellationToken, Task> call, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        await call(cts.Token).WaitAsync(cts.Token);
    }

    private static async Task PublishOfflineAsync(
        IMqttTransport transport, string topic, TimeSpan timeout, ILoggingAdapter log)
    {
        try
        {
            await WithTimeout(ct => transport.SendAsync(topic, "offline", true, ct), timeout);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Publishing offline on {Topic} during stop failed - the Last Will covers this", topic);
        }
    }
}
