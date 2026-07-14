using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain.Analysis;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Mqtt;

namespace Njord.Enrichment.Features;

internal sealed class EnergyEnrichment : IStatelessEnrichment<EnergyResult>
{
    private readonly ResolvedParameterSet _parameters;
    private readonly TimeProvider _timeProvider;
    private readonly EnergyOptions _energyOptions;
    private readonly bool _enabled;

    public string TypeName => "energy";
    public bool Enabled => _enabled;

    public EnergyEnrichment(
        IOptions<EnrichmentOptions> enrichmentOptions,
        ResolvedParameterSet parameters,
        TimeProvider timeProvider)
    {
        _parameters = parameters;
        _timeProvider = timeProvider;
        _energyOptions = enrichmentOptions.Value.Energy;
        _enabled = enrichmentOptions.Value.Energy.Enabled;
    }

    public string DeviceId(string location) =>
        TopicScheme.EnrichmentDeviceId(location, TypeName);

    public IEnumerable<EgressEvent> Compute(ModelSnapshot snapshot, IReadOnlyList<string> locations)
    {
        foreach (var location in locations)
        {
            var result = EnergyResult.Compute(
                snapshot, location, _parameters, _timeProvider, _energyOptions);
            yield return new EgressEvent.EnrichmentUpdate(location, TypeName, result);
        }
    }

    public string BuildDiscoveryPayload(DiscoveryContext ctx, string location)
    {
        var deviceId = DeviceId(location);
        var availabilityTopic = TopicScheme.AvailabilityTopic(ctx.Mqtt.BaseTopic);
        var expireAfterSeconds = (int)(2 * ctx.PollInterval.TotalSeconds);
        var energyTopic = TopicScheme.EnrichmentTopic(ctx.Mqtt.BaseTopic, location, TypeName);

        var components = new JsonObject();

        foreach (var key in new[] { "heating_demand", "cop_estimate", "shading", "night_cooling" })
        {
            components[key] = new JsonObject
            {
                ["p"] = "sensor",
                ["unique_id"] = $"{deviceId}_{key}",
                ["name"] = key.Replace('_', ' '),
                ["expire_after"] = expireAfterSeconds,
                ["value_template"] = $"{{{{ value_json.{key} }}}}",
                ["availability"] = new JsonArray(
                    new JsonObject { ["topic"] = availabilityTopic }),
                ["availability_mode"] = "all",
            };
        }

        components["battery_strategy"] = new JsonObject
        {
            ["p"] = "sensor",
            ["unique_id"] = $"{deviceId}_battery_strategy",
            ["name"] = "battery strategy",
            ["expire_after"] = expireAfterSeconds,
            ["value_template"] = "{{ value_json.battery_strategy }}",
            ["availability"] = new JsonArray(
                new JsonObject { ["topic"] = availabilityTopic }),
            ["availability_mode"] = "all",
        };

        components["cop_optimal"] = new JsonObject
        {
            ["p"] = "sensor",
            ["unique_id"] = $"{deviceId}_cop_optimal",
            ["name"] = "COP optimal hours",
            ["expire_after"] = expireAfterSeconds,
            ["value_template"] = "{{ value_json.cop_optimal | length }}",
            ["json_attributes_topic"] = energyTopic,
            ["json_attributes_template"] = "{{ {'hours': value_json.cop_optimal} | tojson }}",
            ["availability"] = new JsonArray(
                new JsonObject { ["topic"] = availabilityTopic }),
            ["availability_mode"] = "all",
        };

        return DiscoveryPayloadBuilder.BuildDeviceEnvelope(
            deviceId, location, TypeName, ctx.Version, components);
    }

    public IReadOnlyList<MqttMessage> ToStateMessages(object result, string baseTopic, string location)
        => StatePayloadBuilder.FromEnergy((EnergyResult)result, baseTopic);
}
