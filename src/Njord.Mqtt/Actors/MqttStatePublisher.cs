using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Actors;
using Njord.Core.Configuration.Mqtt;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Mqtt.Health;
using Njord.Mqtt.Mapping;
using Njord.Mqtt.Transport;
using Servus.Akka;

namespace Njord.Mqtt.Actors;

public sealed class MqttStatePublisher : ReceiveActor
{
    private readonly string _baseTopic;
    private readonly ResolvedParameterSet _parameters;
    private readonly TimeProvider _timeProvider;
    private readonly IMqttTransport _transport;
    private readonly TimeSpan _publishTimeout;
    private readonly int _eventBufferSize;
    private readonly MqttSessionCounter _sessionCounter;
    private readonly ILoggingAdapter _log = Context.GetLogger();

    private sealed record OutgoingMessage(MqttMessage Message, string Location);

    private sealed record PipelineResolved(IActorRef Pipeline);
    private sealed record PipelineResolveFailed(Exception Cause);

    public MqttStatePublisher(
        IOptions<MqttOptions> mqttOptions,
        ResolvedParameterSet parameters,
        TimeProvider timeProvider,
        IMqttTransport transport,
        MqttEgressTuning tuning,
        MqttSessionCounter sessionCounter)
    {
        _baseTopic = mqttOptions.Value.BaseTopic;
        _parameters = parameters;
        _timeProvider = timeProvider;
        _transport = transport;
        _publishTimeout = tuning.PublishTimeout;
        _eventBufferSize = tuning.EventBufferSize;
        _sessionCounter = sessionCounter;

        Receive<PipelineResolved>(OnPipelineResolved);
        Receive<PipelineResolveFailed>(msg => _log.Warning(msg.Cause, "Failed to resolve pipeline — MQTT publisher has no data"));
        Receive<RequestPipelineOutputCompleted>(OnSourceRef);
        Receive<RequestPipelineOutputFailed>(msg => _log.Warning(msg.Cause, "Pipeline could not create a SourceRef"));
    }

    protected override void PreStart()
    {
        Context.GetActorAsync<IForecastPipeline>().PipeTo(Self,
            success: pipeline => new PipelineResolved(pipeline),
            failure: ex => new PipelineResolveFailed(ex));
    }

    private void OnPipelineResolved(PipelineResolved msg)
    {
        msg.Pipeline.Tell(new RequestPipelineOutput(0));
    }

    private void OnSourceRef(RequestPipelineOutputCompleted msg)
    {
        _log.Info("Pipeline SourceRef received — MQTT publisher attached");
        MaterializeGraph(msg.SourceRef.Source);
    }

    private void MaterializeGraph(Source<PipelineEvent, NotUsed> events)
    {
        var mat = Context.Materializer();
        var baseTopic = _baseTopic;
        var parameters = _parameters;
        var timeProvider = _timeProvider;
        var lastPublished = new PublishedPayloads(_sessionCounter);
        var transport = _transport;
        var publishTimeout = _publishTimeout;
        var log = _log;

        events
            .Buffer(_eventBufferSize, OverflowStrategy.DropHead)
            .Log("mqtt-state-in", e => e switch
            {
                PipelineEvent.WeatherDataReceived w => $"data {w.Location.Name}/weather",
                PipelineEvent.AirQualityDataReceived a => $"data {a.Location.Name}/air-quality",
                PipelineEvent.FloodDataReceived f => $"data {f.Location.Name}/flood",
                PipelineEvent.SolarDataReceived s => $"data {s.Location.Name}/solar",
                PipelineEvent.MarineDataReceived m => $"data {m.Location.Name}/marine",
                PipelineEvent.AlertRaised r => $"enrich {r.Location.Name}/alerts",
                PipelineEvent.AirQualityAlertRaised r => $"enrich {r.Location.Name}/alerts",
                PipelineEvent.FloodAlertRaised r => $"enrich {r.Location.Name}/alerts",
                _ => "?",
            }, _log)
            .SelectMany(pipelineEvent =>
            {
                var context = new MqttPublishContext(baseTopic, parameters, timeProvider.GetUtcNow());
                var location = pipelineEvent.ToLocationName();
                return pipelineEvent.ToMqttMessages(context).Select(message => new OutgoingMessage(message, location));
            })
            .Log("mqtt-state-out", m => $"{m.Message.Topic} [{m.Message.Payload.Length}B]", _log)
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(StreamSupervision.LoggingDecider(_log)))
            .RunWith(Sink.ForEachAsync<OutgoingMessage>(1,
                outgoing => PublishChangedAsync(outgoing, lastPublished, transport, publishTimeout, log)), mat);
    }

    private static async Task PublishChangedAsync(
        OutgoingMessage outgoing,
        PublishedPayloads lastPublished,
        IMqttTransport transport,
        TimeSpan publishTimeout,
        ILoggingAdapter log)
    {
        var (msg, location) = outgoing;
        var locationTag = new KeyValuePair<string, object?>("location", location);
        var hash = msg.Payload.GetHashCode();
        if (lastPublished.IsCurrent(msg.Topic, hash))
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

        lastPublished.Record(msg.Topic, hash);
        Telemetry.MqttDedup.Add(1, locationTag, new KeyValuePair<string, object?>("decision", "published"));
    }

    private sealed class PublishedPayloads(MqttSessionCounter sessionCounter)
    {
        private readonly Dictionary<string, int> _hashes = [];
        private int _session = sessionCounter.Session;

        public bool IsCurrent(string topic, int hash)
        {
            var session = sessionCounter.Session;
            if (session != _session)
            {
                _hashes.Clear();
                _session = session;
            }

            return _hashes.TryGetValue(topic, out var cached) && cached == hash;
        }

        public void Record(string topic, int hash) => _hashes[topic] = hash;
    }
}
