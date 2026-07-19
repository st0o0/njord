using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain.Analysis;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Mqtt;

namespace Njord.Enrichment.Features;

public sealed class ConsensusEnrichment : IStatelessEnrichment
{
    private readonly ResolvedParameterSet _parameters;
    private readonly TimeProvider _timeProvider;
    private readonly double _trimPercent;
    private readonly bool _enabled;
    private readonly int _maxDiscoveryHours;
    private readonly int _maxDiscoveryDays;

    public string TypeName => "consensus";
    public bool Enabled => _enabled;

    public ConsensusEnrichment(
        IOptions<NjordOptions> options,
        IOptions<EnrichmentOptions> enrichmentOptions,
        ResolvedParameterSet parameters,
        TimeProvider timeProvider)
    {
        _parameters = parameters;
        _timeProvider = timeProvider;
        _trimPercent = enrichmentOptions.Value.Consensus.TrimPercent;
        _enabled = enrichmentOptions.Value.Consensus.Enabled;
        _maxDiscoveryHours = options.Value.ForecastDays * 24;
        _maxDiscoveryDays = options.Value.ForecastDays;
    }

    public string DeviceId(string location) =>
        TopicScheme.EnrichmentDeviceId(location, TypeName);

    public IEnumerable<EgressEvent> Compute(ModelSnapshot snapshot, IReadOnlyList<string> locations)
    {
        foreach (var location in locations)
        {
            var cutoffHour = ComputeCutoffHour(snapshot, location);
            if (cutoffHour < 0)
                continue;

            var horizons = Enumerable.Range(0, cutoffHour + 1).ToList();
            var result = ConsensusResult.Compute(
                snapshot, _parameters, horizons, location, _timeProvider, _trimPercent);

            var filtered = FilterByModelCount(result, minModels: 2);
            yield return new EgressEvent.EnrichmentUpdate(location, TypeName, filtered);
        }
    }

    private int ComputeCutoffHour(ModelSnapshot snapshot, string location)
    {
        var now = _timeProvider.GetUtcNow();
        var maxHours = new List<int>();

        foreach (var (key, forecast) in snapshot.Entries)
        {
            if (key.Location != location)
                continue;

            var lastPoint = forecast.Hourly.Points.LastOrDefault();
            if (lastPoint is null)
                continue;

            var hours = (int)Math.Floor((lastPoint.ValidAt - now).TotalHours);
            if (hours > 0)
                maxHours.Add(hours);
        }

        if (maxHours.Count < 2)
            return -1;

        maxHours.Sort();
        return maxHours[^2];
    }

    private static ConsensusResult FilterByModelCount(ConsensusResult result, int minModels)
    {
        return new ConsensusResult(
            FilterParameterList(result.Parameters, minModels),
            FilterParameterList(result.DailyParameters, minModels));
    }

    private static List<ParameterConsensus> FilterParameterList(
        IReadOnlyList<ParameterConsensus> parameters, int minModels)
    {
        var filtered = new List<ParameterConsensus>();

        foreach (var pc in parameters)
        {
            var filteredHorizons = new Dictionary<string, HorizonConsensus>();
            foreach (var (key, hc) in pc.ByHorizon)
            {
                if (hc.AvailableModels.Count >= minModels)
                    filteredHorizons[key] = hc;
            }

            if (filteredHorizons.Count > 0)
                filtered.Add(new ParameterConsensus(pc.Parameter, filteredHorizons));
        }

        return filtered;
    }

    public string BuildDiscoveryPayload(DiscoveryContext ctx, string location)
    {
        var deviceId = DeviceId(location);
        var availabilityTopic = TopicScheme.AvailabilityTopic(ctx.Mqtt.BaseTopic);
        var expireAfterSeconds = (int)(2 * ctx.PollInterval.TotalSeconds);
        var maxHorizon = _maxDiscoveryHours;

        var components = new JsonObject();

        foreach (var parameter in _parameters.Hourly)
        {
            for (var hours = 0; hours <= maxHorizon; hours++)
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

        foreach (var parameter in _parameters.Daily)
        {
            for (var day = 0; day < _maxDiscoveryDays; day++)
            {
                var horizonTopic = TopicScheme.EnrichmentSubTopic(
                    ctx.Mqtt.BaseTopic, location, TypeName, $"d{day}");
                var uniqueId = $"{deviceId}_{parameter.JsonKey}_d{day}";
                var component = DiscoveryPayloadBuilder.BuildComponent(
                    uniqueId,
                    $"{parameter.JsonKey} +{day}d",
                    parameter,
                    $"{{{{ value_json.{parameter.JsonKey} }}}}",
                    horizonTopic,
                    $"{{% if value_json.{parameter.JsonKey} is not none %}}online{{% else %}}offline{{% endif %}}",
                    availabilityTopic,
                    expireAfterSeconds);

                components[$"{parameter.JsonKey}_d{day}"] = component;
            }
        }

        return DiscoveryPayloadBuilder.BuildDeviceEnvelope(
            deviceId, location, TypeName, ctx.Version, components);
    }

    public IReadOnlyList<MqttMessage> ToStateMessages(object result, string baseTopic, string location)
        => StatePayloadBuilder.FromConsensus((ConsensusResult)result, baseTopic, location);
}
