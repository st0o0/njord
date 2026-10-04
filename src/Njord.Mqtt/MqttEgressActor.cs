using System.Diagnostics.Metrics;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Actors;
using Njord.Analysis;
using Njord.Configuration;
using Njord.Diagnostics;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Messages.Egress;
using Servus.Akka;

namespace Njord.Mqtt;

public sealed class MqttEgressActor : StreamConsumerActor
{
    private static readonly Counter<long> DedupMetric = NjordMetrics.Instance.AddMqttDedup();

    private readonly string _baseTopic;
    private readonly ResolvedParameterSet _parameters;
    private readonly IReadOnlyList<int> _horizons;
    private readonly int _forecastDays;
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, IEnrichmentPresenter> _presentersByType;
    private ILoggingAdapter _log = null!;

    private ISinkRef<MqttMessage>? _mqttSinkRef;
    private ISourceRef<EgressEvent>? _egressSourceRef;
    private long _mqttSinkRequestId;
    private long _egressSourceRequestId;

    private sealed record EgressResolved(IActorRef Ref);
    private sealed record ConnectionResolved(IActorRef Ref);
    private sealed record EgressResolveFailed(Exception Cause);
    private sealed record ConnectionResolveFailed(Exception Cause);

    public MqttEgressActor(
        IOptions<NjordOptions> options,
        ResolvedParameterSet parameters,
        TimeProvider timeProvider,
        IEnumerable<IEnrichmentPresenter> presenters)
    {
        var opts = options.Value;
        _baseTopic = opts.Mqtt.BaseTopic;
        _parameters = parameters;
        _horizons = [.. opts.Horizons];
        _forecastDays = opts.ForecastDays;
        _timeProvider = timeProvider;
        _presentersByType = presenters.ToDictionary(p => p.TypeName);
    }

    protected override void PreStart()
    {
        _log = Context.GetLogger();
        base.PreStart();
    }

    protected override void ResolveDependencies()
    {
        Context.GetActorAsync<IEgressActor>().PipeTo(Self, success: r => new EgressResolved(r), failure: ex => new EgressResolveFailed(ex));
        Context.GetActorAsync<IMqttConnectionActor>().PipeTo(Self, success: r => new ConnectionResolved(r), failure: ex => new ConnectionResolveFailed(ex));
    }

    protected override void ConfigureWaitingForRefs()
    {
        Receive<EgressResolved>(msg =>
        {
            if (IsDeadRef(msg.Ref)) { ScheduleRetryResolve(); return; }
            TrackDependency(msg.Ref);
            var id = NextRequestId();
            _egressSourceRequestId = id;
            msg.Ref.Tell(new RequestEgressSource(id));
        });
        Receive<ConnectionResolved>(msg =>
        {
            if (IsDeadRef(msg.Ref)) { ScheduleRetryResolve(); return; }
            TrackDependency(msg.Ref);
            var id = NextRequestId();
            _mqttSinkRequestId = id;
            msg.Ref.Tell(new RequestMqttSink(id));
        });
        Receive<EgressSourceResponse>(response =>
        {
            if (response.RequestId != _egressSourceRequestId) return;
            _egressSourceRef = response.SourceRef;
            _log.Debug("SourceRef received from {Source}", Sender.Path);
            TryTransition();
        });
        Receive<MqttSinkResponse>(response =>
        {
            if (response.RequestId != _mqttSinkRequestId) return;
            _mqttSinkRef = response.SinkRef;
            _log.Debug("SinkRef received from {Source}", Sender.Path);
            TryTransition();
        });
        Receive<EgressSourceFailed>(msg =>
        {
            if (msg.RequestId != _egressSourceRequestId) return;
            _log.Warning(msg.Cause, "Egress source request failed - retrying");
            ScheduleRetryResolve();
        });
        Receive<MqttSinkFailed>(msg =>
        {
            if (msg.RequestId != _mqttSinkRequestId) return;
            _log.Warning(msg.Cause, "MQTT sink request failed - retrying");
            ScheduleRetryResolve();
        });
        Receive<EgressResolveFailed>(msg =>
        {
            _log.Warning(msg.Cause, "Failed to resolve EgressActor - retrying");
            ScheduleRetryResolve();
        });
        Receive<ConnectionResolveFailed>(msg =>
        {
            _log.Warning(msg.Cause, "Failed to resolve MqttConnectionActor - retrying");
            ScheduleRetryResolve();
        });
    }

    protected override bool AllRefsReady() => _egressSourceRef is not null && _mqttSinkRef is not null;

    protected override void MaterializeGraph(SharedKillSwitch killSwitch)
    {
        var baseTopic = _baseTopic;
        var lastPublished = new Dictionary<string, int>();

        _egressSourceRef!.Source
            .Via(killSwitch.Flow<EgressEvent>())
            .Log("mqtt-egress-in", e => e switch
            {
                EgressEvent.PerModelUpdate u => $"model {u.Location}/{u.Model.Id}",
                EgressEvent.EnrichmentUpdate u => $"enrich {u.Location}/{u.TypeName}",
                _ => "?",
            }, _log)
            .SelectMany(egressEvent => MapToMqttMessages(egressEvent, baseTopic, lastPublished))
            .Log("mqtt-egress-out", m => $"{m.Topic} [{m.Payload.Length}B]", _log)
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(StreamSupervision.LoggingDecider(_log)))
            .RunWith(_mqttSinkRef!.Sink, Mat);
    }

    protected override void OnDependencyLost()
    {
        _mqttSinkRef = null;
        _egressSourceRef = null;
        _mqttSinkRequestId = 0;
        _egressSourceRequestId = 0;
    }

    private IEnumerable<MqttMessage> MapToMqttMessages(
        EgressEvent egressEvent, string baseTopic, Dictionary<string, int> lastPublished)
    {
        var messages = egressEvent switch
        {
            EgressEvent.PerModelUpdate e => MapPerModel(e, baseTopic),
            EgressEvent.EnrichmentUpdate e when _presentersByType.TryGetValue(e.TypeName, out var presenter)
                => presenter.ToStateMessages(e.Result, baseTopic, e.Location),
            _ => [],
        };

        var location = egressEvent switch
        {
            EgressEvent.PerModelUpdate u => u.Location,
            EgressEvent.EnrichmentUpdate u => u.Location,
            _ => "unknown",
        };
        var locationTag = new KeyValuePair<string, object?>("location", location);

        foreach (var msg in messages)
        {
            var hash = msg.Payload.GetHashCode();
            if (lastPublished.TryGetValue(msg.Topic, out var cached) && cached == hash)
            {
                DedupMetric.Add(1, locationTag, new KeyValuePair<string, object?>("decision", "skipped"));
                continue;
            }

            lastPublished[msg.Topic] = hash;
            DedupMetric.Add(1, locationTag, new KeyValuePair<string, object?>("decision", "published"));
            yield return msg;
        }
    }

    private IReadOnlyList<MqttMessage> MapPerModel(EgressEvent.PerModelUpdate e, string baseTopic)
    {
        var maxHours = ModelCoverageRegistry.Get(e.Model.Id)?.MaxForecastHours;
        var perHorizon = HorizonProjection.BuildPerHorizon(
            e.Forecast, _parameters, _horizons, _forecastDays, _timeProvider.GetUtcNow(), maxHours);

        var messages = new List<MqttMessage>(perHorizon.Count);
        foreach (var (horizon, payload) in perHorizon)
        {
            var topic = TopicScheme.HorizonTopic(baseTopic, e.Location, e.Model, horizon);
            messages.Add(new MqttMessage(topic, payload, true));
        }
        return messages;
    }
}
