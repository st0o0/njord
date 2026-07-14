using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain.Analysis;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Mqtt;

namespace Njord.Enrichment.Features;

internal sealed class ConsensusEnrichment : IStatelessEnrichment<ConsensusResult>
{
    private readonly ResolvedParameterSet _parameters;
    private readonly IReadOnlyList<int> _horizons;
    private readonly TimeProvider _timeProvider;
    private readonly double _trimPercent;
    private readonly bool _enabled;

    public string TypeName => "consensus";
    public bool Enabled => _enabled;

    public ConsensusEnrichment(
        IOptions<NjordOptions> options,
        IOptions<EnrichmentOptions> enrichmentOptions,
        ResolvedParameterSet parameters,
        TimeProvider timeProvider)
    {
        _parameters = parameters;
        _horizons = [.. options.Value.Horizons];
        _timeProvider = timeProvider;
        _trimPercent = enrichmentOptions.Value.Consensus.TrimPercent;
        _enabled = enrichmentOptions.Value.Consensus.Enabled;
    }

    public string DeviceId(string location) =>
        TopicScheme.EnrichmentDeviceId(location, TypeName);

    public IEnumerable<EgressEvent> Compute(ModelSnapshot snapshot, IReadOnlyList<string> locations)
    {
        foreach (var location in locations)
        {
            var result = ConsensusResult.Compute(
                snapshot, _parameters, _horizons, location, _timeProvider, _trimPercent);
            yield return new EgressEvent.EnrichmentUpdate(location, TypeName, result);
        }
    }

    public string BuildDiscoveryPayload(DiscoveryContext ctx, string location)
    {
        var deviceId = DeviceId(location);
        var availabilityTopic = TopicScheme.AvailabilityTopic(ctx.Mqtt.BaseTopic);
        var expireAfterSeconds = (int)(2 * ctx.PollInterval.TotalSeconds);

        var components = new JsonObject();

        foreach (var parameter in _parameters.Hourly)
        {
            foreach (var hours in _horizons)
            {
                var horizonTopic = TopicScheme.EnrichmentSubTopic(
                    ctx.Mqtt.BaseTopic, location, TypeName, $"h{hours}");
                var uniqueId = $"{deviceId}_{parameter.JsonKey}_h{hours}";
                var component = DiscoveryPayloadBuilder.BuildComponent(
                    uniqueId,
                    $"{parameter.JsonKey} +{hours}h",
                    parameter,
                    $"{{{{ value_json.{parameter.JsonKey} }}}}",
                    horizonTopic,
                    $"{{% if value_json.{parameter.JsonKey} is not none %}}online{{% else %}}offline{{% endif %}}",
                    availabilityTopic,
                    expireAfterSeconds);

                components[$"{parameter.JsonKey}_h{hours}"] = component;
            }
        }

        return DiscoveryPayloadBuilder.BuildDeviceEnvelope(
            deviceId, location, TypeName, ctx.Version, components);
    }

    public IReadOnlyList<MqttMessage> ToStateMessages(object result, string baseTopic, string location)
        => StatePayloadBuilder.FromConsensus((ConsensusResult)result, baseTopic, location);
}
