using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Njord.Compute.Analysis;
using Njord.Core.Configuration;
using Njord.Core.Enrichment;

namespace Njord.Mqtt.Presentation;

internal sealed class TrendPresenter : IEnrichmentPresenter
{
    private static readonly (string Key, string Name)[] TextSensors =
    [
        ("trend_temperature_dir", "temperature trend"),
        ("trend_wind_speed_dir", "wind trend"),
        ("trend_precipitation_dir", "precipitation trend"),
        ("trend_cloud_cover_dir", "cloud cover trend"),
        ("weather_change", "weather change"),
        ("stability", "stability"),
    ];

    private static readonly (string Key, string Name, string Unit)[] NumericSensors =
    [
        ("precip_starts", "precip starts in", "h"),
        ("precip_ends", "precip ends in", "h"),
        ("temp_max_in", "temp max in", "h"),
        ("temp_min_in", "temp min in", "h"),
        ("decay_rate", "decay rate", "°C/h"),
        ("reliable_hours", "reliable hours", "h"),
    ];

    private readonly bool _enabled;

    public string TypeName => EnrichmentTypeNames.Trends;
    public bool Enabled => _enabled;

    public TrendPresenter(IOptions<NjordOptions> options)
    {
        _enabled = options.Value.Enrichment.IsEnabled(TypeName);
    }

    public string DeviceId(string location) =>
        TopicScheme.EnrichmentDeviceId(location, TypeName);

    public string BuildDiscoveryPayload(DiscoveryContext ctx, string location)
    {
        var deviceId = DeviceId(location);
        var availabilityTopic = TopicScheme.AvailabilityTopic(ctx.Mqtt.BaseTopic);
        var expireAfterSeconds = (int)(2 * ctx.PollInterval.TotalSeconds);

        var trendTopic = TopicScheme.EnrichmentTopic(ctx.Mqtt.BaseTopic, location, TypeName);

        var specs = new List<SensorComponentSpec>();

        foreach (var (key, name) in TextSensors)
        {
            specs.Add(new SensorComponentSpec(
                key, $"{deviceId}_{key}", name, trendTopic, $"{{{{ value_json.{key} }}}}"));
        }

        foreach (var (key, name, unit) in NumericSensors)
        {
            specs.Add(new SensorComponentSpec(
                key, $"{deviceId}_{key}", name, trendTopic, $"{{{{ value_json.{key} }}}}", Unit: unit));
        }

        var components = new JsonObject();
        DiscoveryPayloadBuilder.AddComponents(components, specs, availabilityTopic, expireAfterSeconds);

        return DiscoveryPayloadBuilder.BuildDeviceEnvelope(
            deviceId, location, TypeName, ctx.Version, components);
    }

    public IReadOnlyList<MqttMessage> ToStateMessages(object result, string baseTopic, string location)
        => StatePayloadBuilder.FromTrends((TrendResult)result, baseTopic);
}
