using System.Diagnostics.Metrics;
using System.Text.Json.Nodes;
using Akka;
using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Diagnostics;
using Njord.Domain.Analysis;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Mqtt;
using Njord.Pipeline;
using Servus.Akka;

namespace Njord.Enrichment.Features;

internal sealed class HistoryEnrichment : IActorEnrichment
{
    private static readonly Gauge<double> HistoryMae = NjordMetrics.Instance.AddHistoryMae();
    private static readonly Gauge<double> HistoryModelWeight = NjordMetrics.Instance.AddHistoryModelWeight();
    private readonly NjordOptions _njordOptions;
    private readonly ResolvedParameterSet _parameters;
    private readonly TimeProvider _timeProvider;
    private readonly HistoryComputer _computer;
    private readonly HistoryOptions _historyOptions;
    private readonly ILogger<HistoryEnrichment> _logger;
    private readonly bool _enabled;

    public string TypeName => "history";
    public bool Enabled => _enabled;

    public HistoryEnrichment(
        IOptions<NjordOptions> options,
        ResolvedParameterSet parameters,
        TimeProvider timeProvider,
        HistoryComputer computer,
        ILogger<HistoryEnrichment> logger)
    {
        _njordOptions = options.Value;
        _parameters = parameters;
        _timeProvider = timeProvider;
        _computer = computer;
        _historyOptions = options.Value.Enrichment.History;
        _logger = logger;
        _enabled = options.Value.Enrichment.History.Enabled;
    }

    public string DeviceId(string location) =>
        TopicScheme.EnrichmentDeviceId(location, TypeName);

    public Flow<ModelSnapshot, EgressEvent, NotUsed> CreateFlow(IUntypedActorContext context)
    {
        var locations = _njordOptions.Locations.Select(l => l.Name).ToList();
        var computer = _computer;
        var parameters = _parameters;
        var timeProvider = _timeProvider;
        var historyOptions = _historyOptions;

        var historyActors = new Dictionary<string, IActorRef>();
        foreach (var location in locations)
        {
            var actor = context.ResolveChildActor<ForecastHistoryActor>(
                $"forecast-history-{TopicSlug.Slug(location)}",
                location, historyOptions);
            historyActors[location] = actor;
        }

        return Flow.Create<ModelSnapshot>()
            .SelectAsync(1, async snapshot =>
            {
                foreach (var (_, actor) in historyActors)
                    actor.Tell(new RecordSnapshot(snapshot));

                var events = new List<EgressEvent>();
                foreach (var (location, actor) in historyActors)
                {
                    var response = await actor.Ask<HistoryResponse>(new QueryHistory(), TimeSpan.FromSeconds(5));
                    var result = computer.Compute(
                        response.History, snapshot, location, parameters, timeProvider, historyOptions);
                    RecordHistoryMetrics(result);
                    events.Add(new EgressEvent.EnrichmentUpdate(location, TypeName, result));
                }
                return events;
            })
            .SelectMany(events => events)
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(StreamSupervision.LoggingDecider(_logger)));
    }

    public string BuildDiscoveryPayload(DiscoveryContext ctx, string location)
    {
        var deviceId = DeviceId(location);
        var availabilityTopic = TopicScheme.AvailabilityTopic(ctx.Mqtt.BaseTopic);
        var expireAfterSeconds = (int)(2 * ctx.PollInterval.TotalSeconds);
        var modelIds = _njordOptions.Models;

        var historyTopic = TopicScheme.EnrichmentTopic(ctx.Mqtt.BaseTopic, location, TypeName);

        var components = new JsonObject();

        foreach (var modelId in modelIds)
        {
            var slug = modelId.Replace('-', '_').ToLowerInvariant();

            foreach (var (prefix, label) in new[] { ("mae_7d", "MAE 7d"), ("mae_30d", "MAE 30d"), ("weight", "weight"), ("drift", "drift") })
            {
                var key = $"{prefix}_{slug}";
                components[key] = new JsonObject
                {
                    ["p"] = "sensor",
                    ["unique_id"] = $"{deviceId}_{key}",
                    ["name"] = $"{label} {modelId}",
                    ["state_topic"] = historyTopic,
                    ["expire_after"] = expireAfterSeconds,
                    ["value_template"] = $"{{{{ value_json.{key} }}}}",
                    ["availability"] = new JsonArray(
                        new JsonObject { ["topic"] = availabilityTopic }),
                    ["availability_mode"] = "all",
                };
            }
        }

        components["seasonal_best"] = new JsonObject
        {
            ["p"] = "sensor",
            ["unique_id"] = $"{deviceId}_seasonal_best",
            ["name"] = "seasonal best model",
            ["state_topic"] = historyTopic,
            ["expire_after"] = expireAfterSeconds,
            ["value_template"] = "{{ value_json.seasonal_best }}",
            ["availability"] = new JsonArray(
                new JsonObject { ["topic"] = availabilityTopic }),
            ["availability_mode"] = "all",
        };

        components["anomaly"] = new JsonObject
        {
            ["p"] = "binary_sensor",
            ["unique_id"] = $"{deviceId}_anomaly",
            ["name"] = "anomaly",
            ["state_topic"] = historyTopic,
            ["expire_after"] = expireAfterSeconds,
            ["value_template"] = "{% if value_json.anomaly == true %}ON{% else %}OFF{% endif %}",
            ["availability"] = new JsonArray(
                new JsonObject { ["topic"] = availabilityTopic }),
            ["availability_mode"] = "all",
        };

        components["anomaly_deviation"] = new JsonObject
        {
            ["p"] = "sensor",
            ["unique_id"] = $"{deviceId}_anomaly_deviation",
            ["name"] = "anomaly deviation",
            ["state_topic"] = historyTopic,
            ["unit_of_measurement"] = "σ",
            ["expire_after"] = expireAfterSeconds,
            ["value_template"] = "{{ value_json.anomaly_deviation }}",
            ["availability"] = new JsonArray(
                new JsonObject { ["topic"] = availabilityTopic }),
            ["availability_mode"] = "all",
        };

        components["weighted_temperature"] = new JsonObject
        {
            ["p"] = "sensor",
            ["unique_id"] = $"{deviceId}_weighted_temperature",
            ["name"] = "weighted temperature",
            ["state_topic"] = historyTopic,
            ["unit_of_measurement"] = "°C",
            ["device_class"] = "temperature",
            ["expire_after"] = expireAfterSeconds,
            ["value_template"] = "{{ value_json.weighted_temperature }}",
            ["availability"] = new JsonArray(
                new JsonObject { ["topic"] = availabilityTopic }),
            ["availability_mode"] = "all",
        };

        return DiscoveryPayloadBuilder.BuildDeviceEnvelope(
            deviceId, location, TypeName, ctx.Version, components);
    }

    public IReadOnlyList<MqttMessage> ToStateMessages(object result, string baseTopic, string location)
        => StatePayloadBuilder.FromHistory((HistoryResult)result, baseTopic);

    private static void RecordHistoryMetrics(HistoryResult result)
    {
        var locationTag = new KeyValuePair<string, object?>("location", result.Location);
        foreach (var (model, mae) in result.Mae7d)
        {
            if (mae.HasValue)
                HistoryMae.Record(mae.Value, locationTag,
                    new KeyValuePair<string, object?>("model", model.Id));
        }
        foreach (var (model, weight) in result.Weights)
        {
            HistoryModelWeight.Record(weight, locationTag,
                new KeyValuePair<string, object?>("model", model.Id));
        }
    }
}
