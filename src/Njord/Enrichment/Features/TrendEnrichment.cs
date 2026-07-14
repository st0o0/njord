using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain.Analysis;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Mqtt;

namespace Njord.Enrichment.Features;

internal sealed class TrendEnrichment : IStatefulEnrichment<TrendResult>
{
    private readonly ResolvedParameterSet _parameters;
    private readonly IReadOnlyList<int> _horizons;
    private readonly TimeProvider _timeProvider;
    private readonly bool _enabled;

    public string TypeName => "trends";
    public bool Enabled => _enabled;

    public TrendEnrichment(
        IOptions<NjordOptions> options,
        IOptions<EnrichmentOptions> enrichmentOptions,
        ResolvedParameterSet parameters,
        TimeProvider timeProvider)
    {
        _parameters = parameters;
        _horizons = [.. options.Value.Horizons];
        _timeProvider = timeProvider;
        _enabled = enrichmentOptions.Value.Trends.Enabled;
    }

    public string DeviceId(string location) =>
        TopicScheme.EnrichmentDeviceId(location, TypeName);

    public IEnumerable<EgressEvent> Compute(
        ModelSnapshot snapshot, ModelSnapshot? previous, IReadOnlyList<string> locations)
    {
        if (previous is null) yield break;

        foreach (var location in locations)
        {
            var result = TrendResult.Compute(
                snapshot, previous, location, _horizons, _parameters, _timeProvider);
            yield return new EgressEvent.EnrichmentUpdate(location, TypeName, result);
        }
    }

    public string BuildDiscoveryPayload(DiscoveryContext ctx, string location)
    {
        var deviceId = DeviceId(location);
        var availabilityTopic = TopicScheme.AvailabilityTopic(ctx.Mqtt.BaseTopic);
        var expireAfterSeconds = (int)(2 * ctx.PollInterval.TotalSeconds);

        var components = new JsonObject();

        var textSensors = new[]
        {
            ("trend_temperature_dir", "temperature trend"),
            ("trend_wind_speed_dir", "wind trend"),
            ("trend_precipitation_dir", "precipitation trend"),
            ("trend_cloud_cover_dir", "cloud cover trend"),
            ("weather_change", "weather change"),
            ("stability", "stability"),
        };

        foreach (var (key, name) in textSensors)
        {
            components[key] = new JsonObject
            {
                ["p"] = "sensor",
                ["unique_id"] = $"{deviceId}_{key}",
                ["name"] = name,
                ["expire_after"] = expireAfterSeconds,
                ["value_template"] = $"{{{{ value_json.{key} }}}}",
                ["availability"] = new JsonArray(
                    new JsonObject { ["topic"] = availabilityTopic }),
                ["availability_mode"] = "all",
            };
        }

        var numericSensors = new (string Key, string Name, string Unit)[]
        {
            ("precip_starts", "precip starts in", "h"),
            ("precip_ends", "precip ends in", "h"),
            ("temp_max_in", "temp max in", "h"),
            ("temp_min_in", "temp min in", "h"),
            ("decay_rate", "decay rate", "°C/h"),
            ("reliable_hours", "reliable hours", "h"),
        };

        foreach (var (key, name, unit) in numericSensors)
        {
            components[key] = new JsonObject
            {
                ["p"] = "sensor",
                ["unique_id"] = $"{deviceId}_{key}",
                ["name"] = name,
                ["unit_of_measurement"] = unit,
                ["expire_after"] = expireAfterSeconds,
                ["value_template"] = $"{{{{ value_json.{key} }}}}",
                ["availability"] = new JsonArray(
                    new JsonObject { ["topic"] = availabilityTopic }),
                ["availability_mode"] = "all",
            };
        }

        return DiscoveryPayloadBuilder.BuildDeviceEnvelope(
            deviceId, location, TypeName, ctx.Version, components);
    }

    public IReadOnlyList<MqttMessage> ToStateMessages(object result, string baseTopic, string location)
        => StatePayloadBuilder.FromTrends((TrendResult)result, baseTopic);
}
