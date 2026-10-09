using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Njord.Compute.Analysis;
using Njord.Core.Configuration;
using Njord.Core.Enrichment;

namespace Njord.Mqtt.Presentation;

internal sealed class IndexPresenter : IEnrichmentPresenter
{
    private static readonly string[] DayOffsets = ["d0", "d1", "d2"];

    private static readonly string[] ScoreSensors =
        ["laundry", "outdoor", "running", "cycling", "bbq", "irrigation", "solar", "night_ventilation"];

    private static readonly string[] EnvelopeSuffixes = ["min", "max", "confidence"];

    private static readonly (string Key, string Name, string Unit)[] NumericSensors =
    [
        ("frost_hours", "frost in", "h"),
        ("frost_confidence", "frost confidence", ""),
        ("vpd_kpa", "VPD", "kPa"),
    ];

    private readonly bool _enabled;

    public string TypeName => EnrichmentTypeNames.Indices;
    public bool Enabled => _enabled;

    public IndexPresenter(IOptions<NjordOptions> options)
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

        var specs = new List<SensorComponentSpec>();

        foreach (var dayOffset in DayOffsets)
        {
            var dayTopic = TopicScheme.EnrichmentSubTopic(ctx.Mqtt.BaseTopic, location, TypeName, dayOffset);

            foreach (var key in ScoreSensors)
            {
                var compKey = $"{key}_{dayOffset}";
                specs.Add(new SensorComponentSpec(
                    compKey,
                    $"{deviceId}_{compKey}",
                    $"{key.Replace('_', ' ')} {dayOffset}",
                    dayTopic,
                    $"{{{{ value_json.{key} }}}}"));
            }

            foreach (var key in ScoreSensors)
            {
                foreach (var suffix in EnvelopeSuffixes)
                {
                    var envelopeKey = $"{key}_{suffix}_{dayOffset}";
                    specs.Add(new SensorComponentSpec(
                        envelopeKey,
                        $"{deviceId}_{envelopeKey}",
                        $"{key.Replace('_', ' ')} {suffix} {dayOffset}",
                        dayTopic,
                        $"{{{{ value_json.{key}_{suffix} }}}}"));
                }
            }

            var hoursKey = $"hours_included_{dayOffset}";
            specs.Add(new SensorComponentSpec(
                hoursKey,
                $"{deviceId}_{hoursKey}",
                $"hours included {dayOffset}",
                dayTopic,
                "{{ value_json.hours_included }}"));
        }

        var d0Topic = TopicScheme.EnrichmentSubTopic(ctx.Mqtt.BaseTopic, location, TypeName, "d0");

        foreach (var (key, name, unit) in NumericSensors)
        {
            specs.Add(new SensorComponentSpec(
                key,
                $"{deviceId}_{key}",
                name,
                d0Topic,
                $"{{{{ value_json.{key} }}}}",
                Unit: string.IsNullOrEmpty(unit) ? null : unit,
                MeasurementLast: true));
        }

        specs.Add(new SensorComponentSpec(
            "vpd_category",
            $"{deviceId}_vpd_category",
            "VPD category",
            d0Topic,
            "{{ value_json.vpd_category }}"));

        var components = new JsonObject();
        DiscoveryPayloadBuilder.AddComponents(components, specs, availabilityTopic, expireAfterSeconds);

        return DiscoveryPayloadBuilder.BuildDeviceEnvelope(
            deviceId, location, TypeName, ctx.Version, components);
    }

    public IReadOnlyList<MqttMessage> ToStateMessages(object result, string baseTopic, string location)
        => StatePayloadBuilder.FromIndices((IndexResult)result, baseTopic);
}
