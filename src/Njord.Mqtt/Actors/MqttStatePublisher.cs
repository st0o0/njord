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
using Njord.Mqtt.Mapping;
using Njord.Mqtt.Transport;
using Njord.Weather;

namespace Njord.Mqtt.Actors;

public sealed class MqttStatePublisher : StreamConsumerActor
{
    private readonly string _baseTopic;
    private readonly ResolvedParameterSet _parameters;
    private readonly IReadOnlyList<int> _horizons;
    private readonly int _forecastDays;
    private readonly TimeProvider _timeProvider;
    private readonly IMqttTransport _transport;
    private readonly TimeSpan _publishTimeout;
    private readonly int _eventBufferSize;
    private readonly ILoggingAdapter _log = Context.GetLogger();

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
        _eventBufferSize = tuning.EventBufferSize;
    }

    protected override Task MaterializeGraph(Source<PipelineEvent, NotUsed> events)
    {
        var baseTopic = _baseTopic;
        var parameters = _parameters;
        var horizons = _horizons;
        var forecastDays = _forecastDays;
        var timeProvider = _timeProvider;
        // Per subscription on purpose: after a re-attach every state topic is published once again.
        var lastPublished = new Dictionary<string, int>();
        var transport = _transport;
        var publishTimeout = _publishTimeout;
        var log = _log;

        // The buffer always pulls from the pipeline and drops its oldest events while the broker lags behind, so a
        // slow or stuck broker never back-pressures the pipeline output that the other consumers share.
        return events
            .Buffer(_eventBufferSize, OverflowStrategy.DropHead)
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
            .SelectMany(pipelineEvent =>
            {
                var context = new MqttPublishContext(baseTopic, parameters, horizons, forecastDays, timeProvider.GetUtcNow());
                var location = pipelineEvent.ToLocationName();
                return pipelineEvent.ToMqttMessages(context).Select(message => new OutgoingMessage(message, location));
            })
            .Log("mqtt-state-out", m => $"{m.Message.Topic} [{m.Message.Payload.Length}B]", _log)
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(StreamSupervision.LoggingDecider(_log)))
            .RunWith(Sink.ForEachAsync<OutgoingMessage>(1,
                outgoing => PublishChangedAsync(outgoing, lastPublished, transport, publishTimeout, log)), Mat);
    }

    // Dedup check and publish in one step, strictly sequential (ForEachAsync(1)): the cache needs no locking and a
    // message is checked only after the previous publish recorded its hash. The hash is recorded only after the
    // broker accepted the message, so a failed payload is sent again next cycle. Never throws: a failed publish
    // must not end the subscription.
    private static async Task PublishChangedAsync(
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
            return;
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
            return;
        }

        lastPublished[msg.Topic] = hash;
        Telemetry.MqttDedup.Add(1, locationTag, new KeyValuePair<string, object?>("decision", "published"));
    }
}
