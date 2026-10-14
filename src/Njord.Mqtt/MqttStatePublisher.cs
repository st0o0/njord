using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Mqtt;
using Njord.Messages.Pipeline;
using Njord.Mqtt.Transport;
using Njord.Weather;
using Servus.Akka;

namespace Njord.Mqtt;

public sealed class MqttStatePublisher : StreamConsumerActor
{
    private readonly string _baseTopic;
    private readonly ResolvedParameterSet _parameters;
    private readonly IReadOnlyList<int> _horizons;
    private readonly int _forecastDays;
    private readonly TimeProvider _timeProvider;
    private readonly IMqttTransport _transport;
    private readonly TimeSpan _publishTimeout;
    private readonly ILoggingAdapter _log = Context.GetLogger();

    private IActorRef? _pipelineRef;
    private ISourceRef<PipelineEvent>? _pipelineSourceRef;
    private long _pipelineSourceRequestId;

    private sealed record PipelineResolved(IActorRef Ref);
    private sealed record PipelineResolveFailed(Exception Cause);
    private sealed record OutgoingMessage(MqttMessage Message, string Location);

    public MqttStatePublisher(
        IOptions<NjordOptions> options,
        IOptions<MqttOptions> mqttOptions,
        ResolvedParameterSet parameters,
        TimeProvider timeProvider,
        IMqttTransport transport,
        MqttEgressTuning tuning)
    {
        var opts = options.Value;
        _baseTopic = mqttOptions.Value.BaseTopic;
        _parameters = parameters;
        _horizons = [.. opts.Horizons];
        _forecastDays = opts.ForecastDays;
        _timeProvider = timeProvider;
        _transport = transport;
        _publishTimeout = tuning.PublishTimeout;
    }

    protected override void ResolveInitialDependencies()
    {
        _pipelineRef = Context.GetActor<IForecastPipeline>();
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
        Context.GetActorAsync<IForecastPipeline>().PipeTo(Self,
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
        Receive<RequestPipelineOutputCompleted>(response =>
        {
            if (response.RequestId != _pipelineSourceRequestId) return;
            _pipelineSourceRef = response.SourceRef;
            _log.Debug("Pipeline SourceRef received from {Source}", Sender.Path);
            TryTransition();
        });
        Receive<RequestPipelineOutputFailed>(msg =>
        {
            if (msg.RequestId != _pipelineSourceRequestId) return;
            _log.Warning(msg.Cause, "Pipeline source request failed - retrying");
            ScheduleRetryResolve();
        });
        Receive<PipelineResolveFailed>(msg =>
        {
            _log.Warning(msg.Cause, "Failed to resolve ForecastPipeline - retrying");
            ScheduleRetryResolve();
        });
    }

    protected override bool AllRefsReady() => _pipelineSourceRef is not null;

    protected override Task MaterializeGraph(SharedKillSwitch killSwitch)
    {
        var baseTopic = _baseTopic;
        // Per materialization on purpose: after a rewire every state topic is published once again.
        var lastPublished = new Dictionary<string, int>();
        var transport = _transport;
        var publishTimeout = _publishTimeout;
        var log = _log;

        return _pipelineSourceRef!.Source
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
            .SelectMany(pipelineEvent => MapToMqttMessages(pipelineEvent, baseTopic))
            .Log("mqtt-state-out", m => $"{m.Message.Topic} [{m.Message.Payload.Length}B]", _log)
            .SelectAsync(1, outgoing => PublishChangedAsync(outgoing, lastPublished, transport, publishTimeout, log))
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(StreamSupervision.LoggingDecider(_log)))
            .RunWith(Sink.Ignore<NotUsed>(), Mat);
    }

    // Runs strictly sequentially (SelectAsync(1)), so the dedup cache needs no locking. The hash is
    // recorded only after the broker accepted the message: a failed payload is sent again next cycle.
    private static async Task<NotUsed> PublishChangedAsync(
        OutgoingMessage outgoing,
        Dictionary<string, int> lastPublished,
        IMqttTransport transport,
        TimeSpan publishTimeout,
        ILoggingAdapter log)
    {
        var (msg, location) = outgoing;
        var locationTag = new KeyValuePair<string, object?>("location", location);
        var hash = msg.Payload.GetHashCode();
        if (lastPublished.TryGetValue(msg.Topic, out var cached) && cached == hash)
        {
            Telemetry.MqttDedup.Add(1, locationTag, new KeyValuePair<string, object?>("decision", "skipped"));
            return NotUsed.Instance;
        }

        try
        {
            using var timeout = new CancellationTokenSource(publishTimeout);
            await transport.SendAsync(msg.Topic, msg.Payload, msg.Retain, timeout.Token).WaitAsync(timeout.Token);
        }
        catch (Exception ex)
        {
            Telemetry.MqttPublishFailures.Add(1, locationTag);
            log.Warning(ex, "MQTT publish to {Topic} failed - it is sent again with the next cycle", msg.Topic);
            return NotUsed.Instance;
        }

        lastPublished[msg.Topic] = hash;
        Telemetry.MqttDedup.Add(1, locationTag, new KeyValuePair<string, object?>("decision", "published"));
        return NotUsed.Instance;
    }

    protected override void OnDependencyLost()
    {
        _pipelineRef = null;
        _pipelineSourceRef = null;
        _pipelineSourceRequestId = 0;
    }

    protected override void OnStreamLost()
    {
        _pipelineSourceRef = null;
        _pipelineSourceRequestId = 0;
    }

    private IEnumerable<OutgoingMessage> MapToMqttMessages(PipelineEvent pipelineEvent, string baseTopic)
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
            PipelineEvent.ForecastReceived u => u.Location.Name,
            PipelineEvent.ConsensusComputed c => c.Consensus.Location.Name,
            PipelineEvent.AlertComputed a => a.Result.Location.Name,
            PipelineEvent.DerivedComputed d => d.Result.Location.Name,
            PipelineEvent.TrendComputed t => t.Result.Location.Name,
            PipelineEvent.IndexComputed i => i.Result.Location.Name,
            _ => "unknown",
        };

        return messages.Select(msg => new OutgoingMessage(msg, location));
    }

    private IReadOnlyList<MqttMessage> MapPerModel(PipelineEvent.ForecastReceived e, string baseTopic)
    {
        var maxHours = ModelCoverageRegistry.Get(e.Model.Id)?.MaxForecastHours;
        var perHorizon = HorizonProjection.BuildPerHorizon(
            e.Forecast, _parameters, _horizons, _forecastDays, _timeProvider.GetUtcNow(), maxHours);

        var messages = new List<MqttMessage>(perHorizon.Count);
        foreach (var (horizon, payload) in perHorizon)
        {
            var topic = TopicScheme.HorizonTopic(baseTopic, e.Location.Name, e.Model, horizon.ToWireKey());
            messages.Add(new MqttMessage(topic, payload, true));
        }
        return messages;
    }
}
