using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain.Analysis;
using Njord.Domain.Sensors;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Mqtt;

namespace Njord.Enrichment.Features;

internal sealed class IndexEnrichment : IStatelessEnrichment
{
    private readonly IndexComputer _indexComputer;
    private readonly IReadOnlyDictionary<(string Location, string Score), ResolvedPreferences> _resolvedPreferences;
    private readonly bool _enabled;

    public string TypeName => "indices";
    public bool Enabled => _enabled;

    public IndexEnrichment(
        IOptions<NjordOptions> options,
        IndexComputer indexComputer)
    {
        _indexComputer = indexComputer;
        _enabled = options.Value.Enrichment.Indices.Enabled;
        var locationNames = options.Value.Locations.Select(l => l.Name);
        _resolvedPreferences = PreferenceResolver.Resolve(options.Value.Enrichment.Indices, locationNames);
    }

    public string DeviceId(string location) =>
        TopicScheme.EnrichmentDeviceId(location, TypeName);

    public IEnumerable<EgressEvent> Compute(ConsensusSnapshot consensus, SensorSnapshot? sensors = null)
    {
        var prefs = sensors?.Get(SensorKind.IndoorTemperature) is { } liveTemp
            ? OverrideIndoorTemp(_resolvedPreferences, consensus.Location, liveTemp)
            : _resolvedPreferences;

        var result = _indexComputer.Compute(consensus, prefs);
        yield return new EgressEvent.EnrichmentUpdate(consensus.Location, TypeName, result);
    }

    private static IReadOnlyDictionary<(string Location, string Score), ResolvedPreferences> OverrideIndoorTemp(
        IReadOnlyDictionary<(string Location, string Score), ResolvedPreferences> source,
        string location,
        double indoorTemp)
    {
        var result = new Dictionary<(string, string), ResolvedPreferences>(source);
        foreach (var key in source.Keys.Where(k => k.Location == location))
        {
            result[key] = source[key] with { IndoorTemp = indoorTemp };
        }

        return result;
    }

    private static readonly string[] DayOffsets = ["d0", "d1", "d2"];

    private static readonly string[] ScoreSensors =
        ["laundry", "outdoor", "running", "cycling", "bbq", "irrigation", "solar", "night_ventilation"];

    public string BuildDiscoveryPayload(DiscoveryContext ctx, string location)
    {
        var deviceId = DeviceId(location);
        var availabilityTopic = TopicScheme.AvailabilityTopic(ctx.Mqtt.BaseTopic);
        var expireAfterSeconds = (int)(2 * ctx.PollInterval.TotalSeconds);

        var components = new JsonObject();

        foreach (var dayOffset in DayOffsets)
        {
            var dayTopic = TopicScheme.EnrichmentSubTopic(ctx.Mqtt.BaseTopic, location, TypeName, dayOffset);

            foreach (var key in ScoreSensors)
            {
                var compKey = $"{key}_{dayOffset}";
                components[compKey] = new JsonObject
                {
                    ["p"] = "sensor",
                    ["unique_id"] = $"{deviceId}_{compKey}",
                    ["name"] = $"{key.Replace('_', ' ')} {dayOffset}",
                    ["state_topic"] = dayTopic,
                    ["expire_after"] = expireAfterSeconds,
                    ["value_template"] = $"{{{{ value_json.{key} }}}}",
                    ["availability"] = new JsonArray(
                        new JsonObject { ["topic"] = availabilityTopic }),
                    ["availability_mode"] = "all",
                };
            }

            foreach (var key in ScoreSensors)
            {
                foreach (var suffix in new[] { "min", "max", "confidence" })
                {
                    var envelopeKey = $"{key}_{suffix}_{dayOffset}";
                    components[envelopeKey] = new JsonObject
                    {
                        ["p"] = "sensor",
                        ["unique_id"] = $"{deviceId}_{envelopeKey}",
                        ["name"] = $"{key.Replace('_', ' ')} {suffix} {dayOffset}",
                        ["state_topic"] = dayTopic,
                        ["expire_after"] = expireAfterSeconds,
                        ["value_template"] = $"{{{{ value_json.{key}_{suffix} }}}}",
                        ["availability"] = new JsonArray(
                            new JsonObject { ["topic"] = availabilityTopic }),
                        ["availability_mode"] = "all",
                    };
                }
            }

            var hoursKey = $"hours_included_{dayOffset}";
            components[hoursKey] = new JsonObject
            {
                ["p"] = "sensor",
                ["unique_id"] = $"{deviceId}_{hoursKey}",
                ["name"] = $"hours included {dayOffset}",
                ["state_topic"] = dayTopic,
                ["expire_after"] = expireAfterSeconds,
                ["value_template"] = "{{ value_json.hours_included }}",
                ["availability"] = new JsonArray(
                    new JsonObject { ["topic"] = availabilityTopic }),
                ["availability_mode"] = "all",
            };
        }

        var d0Topic = TopicScheme.EnrichmentSubTopic(ctx.Mqtt.BaseTopic, location, TypeName, "d0");

        foreach (var (key, name, unit) in new (string, string, string)[]
                 {
                     ("frost_hours", "frost in", "h"),
                     ("frost_confidence", "frost confidence", ""),
                     ("vpd_kpa", "VPD", "kPa"),
                 })
        {
            var comp = new JsonObject
            {
                ["p"] = "sensor",
                ["unique_id"] = $"{deviceId}_{key}",
                ["name"] = name,
                ["state_topic"] = d0Topic,
                ["expire_after"] = expireAfterSeconds,
                ["value_template"] = $"{{{{ value_json.{key} }}}}",
                ["availability"] = new JsonArray(
                    new JsonObject { ["topic"] = availabilityTopic }),
                ["availability_mode"] = "all",
            };
            if (!string.IsNullOrEmpty(unit))
            {
                comp["unit_of_measurement"] = unit;
            }

            components[key] = comp;
        }

        components["vpd_category"] = new JsonObject
        {
            ["p"] = "sensor",
            ["unique_id"] = $"{deviceId}_vpd_category",
            ["name"] = "VPD category",
            ["state_topic"] = d0Topic,
            ["expire_after"] = expireAfterSeconds,
            ["value_template"] = "{{ value_json.vpd_category }}",
            ["availability"] = new JsonArray(
                new JsonObject { ["topic"] = availabilityTopic }),
            ["availability_mode"] = "all",
        };

        return DiscoveryPayloadBuilder.BuildDeviceEnvelope(
            deviceId, location, TypeName, ctx.Version, components);
    }

    public IReadOnlyList<MqttMessage> ToStateMessages(object result, string baseTopic, string location)
        => StatePayloadBuilder.FromIndices((IndexResult)result, baseTopic);
}
