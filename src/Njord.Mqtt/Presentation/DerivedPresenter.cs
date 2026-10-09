using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Njord.Compute.Analysis;
using Njord.Core.Configuration;
using Njord.Core.Enrichment;

namespace Njord.Mqtt.Presentation;

internal sealed class DerivedPresenter : IEnrichmentPresenter
{
    private readonly IReadOnlyList<int> _horizons;
    private readonly bool _enabled;

    public string TypeName => EnrichmentTypeNames.Derived;
    public bool Enabled => _enabled;

    public DerivedPresenter(IOptions<NjordOptions> options)
    {
        _horizons = [.. options.Value.Horizons];
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

        var horizonParams = new (string Key, string Name, string? Unit, string? DeviceClass)[]
        {
            ("beaufort", "beaufort", null, null),
            ("wind_chill", "wind chill", "°C", "temperature"),
            ("dewpoint_comfort", "dew-point comfort", null, null),
            ("wmo_description", "weather", null, null),
        };

        foreach (var hours in _horizons)
        {
            var horizonTopic = TopicScheme.EnrichmentSubTopic(
                ctx.Mqtt.BaseTopic, location, TypeName, $"h{hours}");

            foreach (var (key, name, unit, deviceClass) in horizonParams)
            {
                specs.Add(new SensorComponentSpec(
                    $"{key}_h{hours}",
                    $"{deviceId}_{key}_h{hours}",
                    $"{name} +{hours}h",
                    horizonTopic,
                    $"{{{{ value_json.{key} }}}}",
                    Unit: unit,
                    DeviceClass: deviceClass,
                    ExtraAvailability:
                    [
                        (horizonTopic,
                            $"{{% if value_json.{key} is not none %}}online{{% else %}}offline{{% endif %}}"),
                    ],
                    MeasurementLast: true));
            }
        }

        var metaTopic = TopicScheme.EnrichmentSubTopic(ctx.Mqtt.BaseTopic, location, TypeName, "meta");

        specs.Add(new SensorComponentSpec(
            "diurnal_amplitude",
            $"{deviceId}_diurnal_amplitude",
            "diurnal amplitude",
            metaTopic,
            "{{ value_json.diurnal_amplitude }}",
            Unit: "°C",
            DeviceClass: "temperature"));

        specs.Add(new SensorComponentSpec(
            "sunshine_pct",
            $"{deviceId}_sunshine_pct",
            "sunshine",
            metaTopic,
            "{{ value_json.sunshine_pct }}",
            Unit: "%"));

        specs.Add(new SensorComponentSpec(
            "inversion",
            $"{deviceId}_inversion",
            "inversion",
            metaTopic,
            "{% if value_json.inversion == true %}ON{% else %}OFF{% endif %}",
            Platform: "binary_sensor"));

        var components = new JsonObject();
        DiscoveryPayloadBuilder.AddComponents(components, specs, availabilityTopic, expireAfterSeconds);

        return DiscoveryPayloadBuilder.BuildDeviceEnvelope(
            deviceId, location, TypeName, ctx.Version, components);
    }

    public IReadOnlyList<MqttMessage> ToStateMessages(object result, string baseTopic, string location)
        => StatePayloadBuilder.FromDerived((DerivedResult)result, baseTopic);
}
