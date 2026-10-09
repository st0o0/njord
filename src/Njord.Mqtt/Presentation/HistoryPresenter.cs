using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Njord.Compute.Analysis;
using Njord.Core.Configuration;
using Njord.Core.Enrichment;

namespace Njord.Mqtt.Presentation;

internal sealed class HistoryPresenter : IEnrichmentPresenter
{
    private static readonly (string Prefix, string Label)[] PerModelSensors =
    [
        ("mae_7d", "MAE 7d"),
        ("mae_30d", "MAE 30d"),
        ("weight", "weight"),
        ("drift", "drift"),
    ];

    private readonly IReadOnlyList<string> _models;
    private readonly bool _enabled;

    public string TypeName => EnrichmentTypeNames.History;
    public bool Enabled => _enabled;

    public HistoryPresenter(IOptions<NjordOptions> options)
    {
        _models = [.. options.Value.Models];
        _enabled = options.Value.Enrichment.IsEnabled(TypeName);
    }

    public string DeviceId(string location) =>
        TopicScheme.EnrichmentDeviceId(location, TypeName);

    public string BuildDiscoveryPayload(DiscoveryContext ctx, string location)
    {
        var deviceId = DeviceId(location);
        var availabilityTopic = TopicScheme.AvailabilityTopic(ctx.Mqtt.BaseTopic);
        var expireAfterSeconds = (int)(2 * ctx.PollInterval.TotalSeconds);

        var historyTopic = TopicScheme.EnrichmentTopic(ctx.Mqtt.BaseTopic, location, TypeName);

        var specs = new List<SensorComponentSpec>();

        foreach (var modelId in _models)
        {
            var slug = modelId.Replace('-', '_').ToLowerInvariant();

            foreach (var (prefix, label) in PerModelSensors)
            {
                var key = $"{prefix}_{slug}";
                specs.Add(new SensorComponentSpec(
                    key, $"{deviceId}_{key}", $"{label} {modelId}", historyTopic, $"{{{{ value_json.{key} }}}}"));
            }
        }

        specs.Add(new SensorComponentSpec(
            "seasonal_best",
            $"{deviceId}_seasonal_best",
            "seasonal best model",
            historyTopic,
            "{{ value_json.seasonal_best }}"));

        specs.Add(new SensorComponentSpec(
            "anomaly",
            $"{deviceId}_anomaly",
            "anomaly",
            historyTopic,
            "{% if value_json.anomaly == true %}ON{% else %}OFF{% endif %}",
            Platform: "binary_sensor"));

        specs.Add(new SensorComponentSpec(
            "anomaly_deviation",
            $"{deviceId}_anomaly_deviation",
            "anomaly deviation",
            historyTopic,
            "{{ value_json.anomaly_deviation }}",
            Unit: "σ"));

        specs.Add(new SensorComponentSpec(
            "weighted_temperature",
            $"{deviceId}_weighted_temperature",
            "weighted temperature",
            historyTopic,
            "{{ value_json.weighted_temperature }}",
            Unit: "°C",
            DeviceClass: "temperature"));

        var components = new JsonObject();
        DiscoveryPayloadBuilder.AddComponents(components, specs, availabilityTopic, expireAfterSeconds);

        return DiscoveryPayloadBuilder.BuildDeviceEnvelope(
            deviceId, location, TypeName, ctx.Version, components);
    }

    public IReadOnlyList<MqttMessage> ToStateMessages(object result, string baseTopic, string location)
        => StatePayloadBuilder.FromHistory((HistoryResult)result, baseTopic);
}
