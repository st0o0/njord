using System.Diagnostics.Metrics;
using Akka;
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
using Njord.Mqtt.Transport;
using Servus.Akka;

namespace Njord.Mqtt;

public sealed class MqttStateActor : StreamConsumerActor
{
    private static readonly Counter<long> DedupMetric = NjordMetrics.Instance.AddMqttDedup();

    private readonly string _baseTopic;
    private readonly ResolvedParameterSet _parameters;
    private readonly IReadOnlyList<int> _horizons;
    private readonly int _forecastDays;
    private readonly TimeProvider _timeProvider;
    private readonly IMqttTransport _transport;
    private readonly Dictionary<string, IEnrichmentPresenter> _presentersByType;
    private ILoggingAdapter _log = null!;

    private IActorRef? _modelStateRef;
    private IActorRef? _enrichmentRef;
    private ISourceRef<EgressEvent>? _modelStateSourceRef;
    private ISourceRef<EgressEvent>? _enrichmentSourceRef;
    private long _modelStateSourceRequestId;
    private long _enrichmentSourceRequestId;

    private sealed record ModelStateResolved(IActorRef Ref);
    private sealed record EnrichmentResolved(IActorRef Ref);
    private sealed record ModelStateResolveFailed(Exception Cause);
    private sealed record EnrichmentResolveFailed(Exception Cause);

    public MqttStateActor(
        IOptions<NjordOptions> options,
        IOptions<MqttOptions> mqttOptions,
        ResolvedParameterSet parameters,
        TimeProvider timeProvider,
        IMqttTransport transport,
        IEnumerable<IEnrichmentPresenter> presenters)
    {
        var opts = options.Value;
        _baseTopic = mqttOptions.Value.BaseTopic;
        _parameters = parameters;
        _horizons = [.. opts.Horizons];
        _forecastDays = opts.ForecastDays;
        _timeProvider = timeProvider;
        _transport = transport;
        _presentersByType = presenters.ToDictionary(p => p.TypeName);
    }

    protected override void PreStart()
    {
        _log = Context.GetLogger();
        base.PreStart();
    }

    protected override void ResolveInitialDependencies()
    {
        _modelStateRef = Context.GetActor<IModelStateActor>();
        TrackDependency(_modelStateRef);

        _enrichmentRef = Context.GetActor<IEnrichmentActor>();
        TrackDependency(_enrichmentRef);
    }

    protected override void RequestSourceRefs()
    {
        var modelId = NextRequestId();
        _modelStateSourceRequestId = modelId;
        _modelStateRef!.Tell(new RequestModelStateSource(modelId));

        var enrichId = NextRequestId();
        _enrichmentSourceRequestId = enrichId;
        _enrichmentRef!.Tell(new RequestEnrichmentSource(enrichId));
    }

    protected override void ResolveDependencies()
    {
        Context.GetActorAsync<IModelStateActor>().PipeTo(Self, success: r => new ModelStateResolved(r), failure: ex => new ModelStateResolveFailed(ex));
        Context.GetActorAsync<IEnrichmentActor>().PipeTo(Self, success: r => new EnrichmentResolved(r), failure: ex => new EnrichmentResolveFailed(ex));
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
        Receive<EnrichmentResolved>(msg =>
        {
            if (IsDeadRef(msg.Ref)) { ScheduleRetryResolve(); return; }
            TrackDependency(msg.Ref);
            var id = NextRequestId();
            _enrichmentSourceRequestId = id;
            msg.Ref.Tell(new RequestEnrichmentSource(id));
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
        Receive<EnrichmentSourceResponse>(response =>
        {
            if (response.RequestId != _enrichmentSourceRequestId)
            {
                return;
            }

            _enrichmentSourceRef = response.SourceRef;
            _log.Debug("Enrichment SourceRef received from {Source}", Sender.Path);
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
        Receive<EnrichmentSourceFailed>(msg =>
        {
            if (msg.RequestId != _enrichmentSourceRequestId)
            {
                return;
            }

            _log.Warning(msg.Cause, "Enrichment source request failed - retrying");
            ScheduleRetryResolve();
        });
        Receive<ModelStateResolveFailed>(msg =>
        {
            _log.Warning(msg.Cause, "Failed to resolve ModelStateActor - retrying");
            ScheduleRetryResolve();
        });
        Receive<EnrichmentResolveFailed>(msg =>
        {
            _log.Warning(msg.Cause, "Failed to resolve EnrichmentActor - retrying");
            ScheduleRetryResolve();
        });
    }

    protected override bool AllRefsReady() => _modelStateSourceRef is not null && _enrichmentSourceRef is not null;

    protected override void MaterializeGraph(SharedKillSwitch killSwitch)
    {
        var baseTopic = _baseTopic;
        var lastPublished = new Dictionary<string, int>();
        var transport = _transport;

        _modelStateSourceRef!.Source
            .Merge(_enrichmentSourceRef!.Source)
            .Via(killSwitch.Flow<EgressEvent>())
            .Log("mqtt-state-in", e => e switch
            {
                EgressEvent.PerModelUpdate u => $"model {u.Location}/{u.Model.Id}",
                EgressEvent.EnrichmentUpdate u => $"enrich {u.Location}/{u.TypeName}",
                _ => "?",
            }, _log)
            .SelectMany(egressEvent => MapToMqttMessages(egressEvent, baseTopic, lastPublished))
            .Log("mqtt-state-out", m => $"{m.Topic} [{m.Payload.Length}B]", _log)
            .SelectAsync(1, async msg =>
            {
                await transport.SendAsync(msg.Topic, msg.Payload, msg.Retain, CancellationToken.None);
                return msg;
            })
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(StreamSupervision.LoggingDecider(_log)))
            .RunWith(Sink.Ignore<MqttMessage>(), Mat);
    }

    protected override void OnDependencyLost()
    {
        _modelStateRef = null;
        _enrichmentRef = null;
        _modelStateSourceRef = null;
        _enrichmentSourceRef = null;
        _modelStateSourceRequestId = 0;
        _enrichmentSourceRequestId = 0;
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
