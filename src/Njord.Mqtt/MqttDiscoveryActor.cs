using System.Reflection;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Actors;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Messages.Egress;
using Njord.Messages.Mqtt;
using Njord.Mqtt.Transport;
using Servus.Akka;

namespace Njord.Mqtt;

public sealed class MqttDiscoveryActor : StreamConsumerActor, IWithTimers
{
    public ITimerScheduler Timers { get; set; } = null!;
    private static readonly string Version =
        typeof(MqttDiscoveryActor).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "unknown";

    private readonly NjordOptions _options;
    private readonly MqttOptions _mqttOptions;
    private readonly ResolvedParameterSet _parameters;
    private readonly IReadOnlyList<IEnrichmentPresenter> _presenters;
    private readonly IMqttTransport _transport;
    private readonly string _haStatusTopic;
    private readonly bool _discoveryEnabled;
    private readonly int _expectedModelCount;
    private ILoggingAdapter _log = null!;

    private IActorRef? _modelStateRef;
    private ISourceRef<EgressEvent>? _modelStateSourceRef;
    private long _modelStateSourceRequestId;
    private readonly Dictionary<(string Location, string ModelId), EgressEvent.CapabilityLearned> _capabilities = new();
    private bool _initialDiscoveryPublished;

    private sealed record ModelStateResolved(IActorRef Ref);
    private sealed record ConnectionResolved(IActorRef Ref);
    private sealed record ModelStateResolveFailed(Exception Cause);
    private sealed record ConnectionResolveFailed(Exception Cause);
    private sealed record StreamCompleted
    {
        public static readonly StreamCompleted Instance = new();
    }

    public MqttDiscoveryActor(
        IOptions<NjordOptions> options,
        IOptions<MqttOptions> mqttOptions,
        ResolvedParameterSet parameters,
        IMqttTransport transport,
        IEnumerable<IEnrichmentPresenter> presenters)
    {
        _options = options.Value;
        _mqttOptions = mqttOptions.Value;
        _parameters = parameters;
        _transport = transport;
        _presenters = [.. presenters];
        _haStatusTopic = $"{_mqttOptions.DiscoveryPrefix}/status";
        _discoveryEnabled = _mqttOptions.DiscoveryEnabled;

        _expectedModelCount = _options.Locations
            .Sum(loc => _options.Models.Union(loc.Models ?? [], StringComparer.OrdinalIgnoreCase).Count());
    }

    protected override void PreStart()
    {
        _log = Context.GetLogger();

        if (!_discoveryEnabled)
        {
            _log.Info("MQTT discovery is disabled — MqttDiscoveryActor idle");
            return;
        }

        base.PreStart();
    }

    protected override void ResolveInitialDependencies()
    {
        _modelStateRef = Context.GetActor<IModelStateActor>();
        TrackDependency(_modelStateRef);

        var connection = Context.GetActor<IMqttConnectionActor>();
        TrackDependency(connection);
        connection.Tell(new SubscribeInbound(Self));
    }

    protected override void RequestSourceRefs()
    {
        var id = NextRequestId();
        _modelStateSourceRequestId = id;
        _modelStateRef!.Tell(new RequestModelStateSource(id));
    }

    protected override void ResolveDependencies()
    {
        Context.GetActorAsync<IModelStateActor>().PipeTo(Self, success: r => new ModelStateResolved(r), failure: ex => new ModelStateResolveFailed(ex));
        Context.GetActorAsync<IMqttConnectionActor>().PipeTo(Self, success: r => new ConnectionResolved(r), failure: ex => new ConnectionResolveFailed(ex));
    }

    protected override void ConfigureWaitingForRefs()
    {
        Receive<ModelStateResolved>(msg =>
        {
            if (IsDeadRef(msg.Ref)) { ScheduleRetryResolve(); return; }
            TrackDependency(msg.Ref);
            var id = NextRequestId();
            _modelStateSourceRequestId = id;
            msg.Ref.Tell(new RequestModelStateSource(id));
        });
        Receive<ConnectionResolved>(msg =>
        {
            if (IsDeadRef(msg.Ref)) { ScheduleRetryResolve(); return; }
            TrackDependency(msg.Ref);
            msg.Ref.Tell(new SubscribeInbound(Self));
            TryTransition();
        });
        Receive<ModelStateSourceResponse>(response =>
        {
            if (response.RequestId != _modelStateSourceRequestId)
            {
                return;
            }

            _modelStateSourceRef = response.SourceRef;
            _log.Debug("ModelState SourceRef received from {Source}", Sender.Path);
            TryTransition();
        });
        Receive<ModelStateSourceFailed>(msg =>
        {
            if (msg.RequestId != _modelStateSourceRequestId)
            {
                return;
            }

            _log.Warning(msg.Cause, "ModelState source request failed - retrying");
            ScheduleRetryResolve();
        });
        Receive<ModelStateResolveFailed>(msg =>
        {
            _log.Warning(msg.Cause, "Failed to resolve ModelStateActor - retrying");
            ScheduleRetryResolve();
        });
        Receive<ConnectionResolveFailed>(msg =>
        {
            _log.Warning(msg.Cause, "Failed to resolve MqttConnectionActor - retrying");
            ScheduleRetryResolve();
        });
    }

    protected override bool AllRefsReady() => _modelStateSourceRef is not null;

    protected override void MaterializeGraph(SharedKillSwitch killSwitch)
    {
        var self = Self;
        _modelStateSourceRef!.Source
            .Via(killSwitch.Flow<EgressEvent>())
            .Where(e => e is EgressEvent.CapabilityLearned)
            .Log("discovery-capability", e => $"{((EgressEvent.CapabilityLearned)e).Location}/{((EgressEvent.CapabilityLearned)e).Model.Id}", _log)
            .Select(e => new CapabilityReceived((EgressEvent.CapabilityLearned)e))
            .RunWith(Sink.ActorRef<CapabilityReceived>(self, StreamCompleted.Instance, _ => StreamCompleted.Instance), Mat);
    }

    protected override void ConfigureReady()
    {
        _log.Info("MqttDiscoveryActor ready — waiting for model capabilities");
        ScheduleCapabilityTimeout();

        Receive<CapabilityReceived>(msg =>
        {
            if (!_initialDiscoveryPublished)
            {
                OnCapabilityLearned(msg.Event);
            }
            else
            {
                OnCapabilityUpdate(msg.Event);
            }
        });
        Receive<CapabilityTimeout>(_ => OnCapabilityTimeout());
        Receive<MqttConnected>(_ => { });
        Receive<MqttInboundMessage>(OnInbound);
        Receive<StreamCompleted>(_ => { });
    }

    protected override void OnDependencyLost()
    {
        _modelStateRef = null;
        _modelStateSourceRef = null;
        _modelStateSourceRequestId = 0;
    }

    private void OnCapabilityLearned(EgressEvent.CapabilityLearned msg)
    {
        _capabilities[(msg.Location, msg.Model.Id)] = msg;
        _log.Info(
            "Capability received for {Location}/{Model} ({Count}/{Expected})",
            msg.Location, msg.Model.Id, _capabilities.Count, _expectedModelCount);

        if (_capabilities.Count >= _expectedModelCount)
        {
            PublishDiscovery();
            _initialDiscoveryPublished = true;
        }
    }

    private void OnCapabilityTimeout()
    {
        if (_initialDiscoveryPublished)
        {
            return;
        }

        _log.Warning(
            "Capability timeout — publishing discovery for {Count}/{Expected} models",
            _capabilities.Count, _expectedModelCount);

        PublishDiscovery();
        _initialDiscoveryPublished = true;
    }

    private void OnCapabilityUpdate(EgressEvent.CapabilityLearned msg)
    {
        var key = (msg.Location, msg.Model.Id);
        var isNew = !_capabilities.ContainsKey(key);
        _capabilities[key] = msg;

        if (isNew)
        {
            _log.Info("Late capability for {Location}/{Model} — publishing discovery", msg.Location, msg.Model.Id);
        }
        else
        {
            _log.Info("Capability expanded for {Location}/{Model} — re-publishing discovery", msg.Location, msg.Model.Id);
        }

        PublishDiscoveryForModel(msg);
    }

    private void OnInbound(MqttInboundMessage message)
    {
        if (message.Topic == _haStatusTopic && message.Payload == "online")
        {
            _log.Info("Home Assistant is back online — re-publishing discovery");
            PublishDiscovery();
        }
    }

    private void PublishDiscovery()
    {
        var ctx = new DiscoveryContext(_mqttOptions, _options.PollInterval, Version);

        foreach (var location in _options.Locations)
        {
            foreach (var modelId in _options.Models.Union(location.Models ?? [], StringComparer.OrdinalIgnoreCase))
            {
                var key = (location.Name, modelId);
                if (!_capabilities.TryGetValue(key, out var cap))
                {
                    continue;
                }

                PublishDiscoveryForModel(cap);
            }

            foreach (var presenter in _presenters)
            {
                if (!presenter.Enabled)
                {
                    continue;
                }

                var deviceId = presenter.DeviceId(location.Name);
                var topic = TopicScheme.ConfigTopic(_mqttOptions.DiscoveryPrefix, deviceId);
                var payload = presenter.BuildDiscoveryPayload(ctx, location.Name);
                _transport.SendAsync(topic, payload, true, CancellationToken.None);
            }
        }
    }

    private void PublishDiscoveryForModel(EgressEvent.CapabilityLearned cap)
    {
        var model = cap.Model;
        var topic = TopicScheme.ConfigTopic(
            _mqttOptions.DiscoveryPrefix, TopicScheme.DeviceId(cap.Location, model));
        var payload = DiscoveryPayloadBuilder.Build(
            cap.Location, model, _parameters,
            cap.ApplicableHorizons, cap.ApplicableDayOffsets,
            cap.SupportedParameters,
            _mqttOptions, _options.PollInterval, Version);
        _transport.SendAsync(topic, payload, true, CancellationToken.None);
    }

    private void ScheduleCapabilityTimeout()
    {
        var timeout = _options.PollInterval + _options.PollInterval;
        Timers.StartSingleTimer("capability-timeout", new CapabilityTimeout(), timeout);
    }

    private sealed record CapabilityTimeout;
    private sealed record CapabilityReceived(EgressEvent.CapabilityLearned Event);
}
