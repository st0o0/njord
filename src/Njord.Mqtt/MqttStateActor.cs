using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Compute.Analysis;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Egress;
using Njord.Core.Egress;
using Njord.Mqtt.Transport;
using Njord.Weather;
using Servus.Akka;

namespace Njord.Mqtt;

public sealed class MqttStateActor : StreamConsumerActor
{
    private readonly string _baseTopic;
    private readonly ResolvedParameterSet _parameters;
    private readonly IReadOnlyList<int> _horizons;
    private readonly int _forecastDays;
    private readonly TimeProvider _timeProvider;
    private readonly IMqttTransport _transport;
    private ILoggingAdapter _log = null!;

    private IActorRef? _pipelineRef;
    private ISourceRef<PipelineEvent>? _pipelineSourceRef;
    private long _pipelineSourceRequestId;

    private sealed record PipelineResolved(IActorRef Ref);
    private sealed record PipelineResolveFailed(Exception Cause);

    public MqttStateActor(
        IOptions<NjordOptions> options,
        IOptions<MqttOptions> mqttOptions,
        ResolvedParameterSet parameters,
        TimeProvider timeProvider,
        IMqttTransport transport)
    {
        var opts = options.Value;
        _baseTopic = mqttOptions.Value.BaseTopic;
        _parameters = parameters;
        _horizons = [.. opts.Horizons];
        _forecastDays = opts.ForecastDays;
        _timeProvider = timeProvider;
        _transport = transport;
    }

    protected override void PreStart()
    {
        _log = Context.GetLogger();
        base.PreStart();
    }

    protected override void ResolveInitialDependencies()
    {
        _pipelineRef = Context.GetActor<IPipelineActor>();
        TrackDependency(_pipelineRef);
    }

    protected override void RequestSourceRefs()
    {
        var id = NextRequestId();
        _pipelineSourceRequestId = id;
        _pipelineRef!.Tell(new RequestPipelineOutput(id));
    }

    protected override void ResolveDependencies()
    {
        Context.GetActorAsync<IPipelineActor>().PipeTo(Self,
            success: r => new PipelineResolved(r),
            failure: ex => new PipelineResolveFailed(ex));
    }

    protected override void ConfigureWaitingForRefs()
    {
        Receive<PipelineResolved>(msg =>
        {
            if (IsDeadRef(msg.Ref)) { ScheduleRetryResolve(); return; }
            TrackDependency(msg.Ref);
            var id = NextRequestId();
            _pipelineSourceRequestId = id;
            msg.Ref.Tell(new RequestPipelineOutput(id));
        });
        Receive<PipelineOutputResponse>(response =>
        {
            if (response.RequestId != _pipelineSourceRequestId) return;
            _pipelineSourceRef = response.SourceRef;
            _log.Debug("Pipeline SourceRef received from {Source}", Sender.Path);
            TryTransition();
        });
        Receive<PipelineOutputFailed>(msg =>
        {
            if (msg.RequestId != _pipelineSourceRequestId) return;
            _log.Warning(msg.Cause, "Pipeline source request failed - retrying");
            ScheduleRetryResolve();
        });
        Receive<PipelineResolveFailed>(msg =>
        {
            _log.Warning(msg.Cause, "Failed to resolve PipelineActor - retrying");
            ScheduleRetryResolve();
        });
    }

    protected override bool AllRefsReady() => _pipelineSourceRef is not null;

    protected override void MaterializeGraph(SharedKillSwitch killSwitch)
    {
        var baseTopic = _baseTopic;
        var lastPublished = new Dictionary<string, int>();
        var transport = _transport;

        _pipelineSourceRef!.Source
            .Via(killSwitch.Flow<PipelineEvent>())
            .Log("mqtt-state-in", e => e switch
            {
                PipelineEvent.ForecastReceived u => $"model {u.Location}/{u.Model.Id}",
                PipelineEvent.ConsensusComputed c => $"enrich {c.Consensus.Location}/consensus",
                PipelineEvent.AlertComputed a => $"enrich {a.Result.Location}/alerts",
                PipelineEvent.DerivedComputed d => $"enrich {d.Result.Location}/derived",
                PipelineEvent.TrendComputed t => $"enrich {t.Result.Location}/trends",
                PipelineEvent.IndexComputed i => $"enrich {i.Result.Location}/indices",
                _ => "?",
            }, _log)
            .SelectMany(pipelineEvent => MapToMqttMessages(pipelineEvent, baseTopic, lastPublished))
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
        _pipelineRef = null;
        _pipelineSourceRef = null;
        _pipelineSourceRequestId = 0;
    }

    private IEnumerable<MqttMessage> MapToMqttMessages(
        PipelineEvent pipelineEvent, string baseTopic, Dictionary<string, int> lastPublished)
    {
        var messages = pipelineEvent switch
        {
            PipelineEvent.ForecastReceived e => MapPerModel(e, baseTopic),
            PipelineEvent.ConsensusComputed c => StatePayloadBuilder.FromConsensus(c.Consensus, baseTopic),
            PipelineEvent.AlertComputed a => StatePayloadBuilder.FromAlerts(a.Result, baseTopic),
            PipelineEvent.DerivedComputed d => StatePayloadBuilder.FromDerived(d.Result, baseTopic),
            PipelineEvent.TrendComputed t => StatePayloadBuilder.FromTrends(t.Result, baseTopic),
            PipelineEvent.IndexComputed i => StatePayloadBuilder.FromIndices(i.Result, baseTopic),
            _ => [],
        };

        var location = pipelineEvent switch
        {
            PipelineEvent.ForecastReceived u => u.Location,
            PipelineEvent.ConsensusComputed c => c.Consensus.Location,
            PipelineEvent.AlertComputed a => a.Result.Location,
            PipelineEvent.DerivedComputed d => d.Result.Location,
            PipelineEvent.TrendComputed t => t.Result.Location,
            PipelineEvent.IndexComputed i => i.Result.Location,
            _ => "unknown",
        };
        var locationTag = new KeyValuePair<string, object?>("location", location);

        foreach (var msg in messages)
        {
            var hash = msg.Payload.GetHashCode();
            if (lastPublished.TryGetValue(msg.Topic, out var cached) && cached == hash)
            {
                Telemetry.MqttDedup.Add(1, locationTag, new KeyValuePair<string, object?>("decision", "skipped"));
                continue;
            }

            lastPublished[msg.Topic] = hash;
            Telemetry.MqttDedup.Add(1, locationTag, new KeyValuePair<string, object?>("decision", "published"));
            yield return msg;
        }
    }

    private IReadOnlyList<MqttMessage> MapPerModel(PipelineEvent.ForecastReceived e, string baseTopic)
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
