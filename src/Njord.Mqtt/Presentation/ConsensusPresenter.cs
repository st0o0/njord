using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain.Analysis;
using Njord.Domain.Weather;
using Njord.Enrichment;

namespace Njord.Mqtt.Presentation;

internal sealed class ConsensusPresenter : IEnrichmentPresenter
{
    private readonly ResolvedParameterSet _parameters;
    private readonly int _forecastDays;

    public string TypeName => EnrichmentTypeNames.Consensus;

    // Consensus discovery is unconditional today: Consensus.Enabled does not gate it.
    public bool Enabled => true;

    public ConsensusPresenter(IOptions<NjordOptions> options, ResolvedParameterSet parameters)
    {
        _parameters = parameters;
        _forecastDays = options.Value.ForecastDays;
    }

    public string DeviceId(string location) =>
        TopicScheme.EnrichmentDeviceId(location, TypeName);

    public string BuildDiscoveryPayload(DiscoveryContext ctx, string location) =>
        DiscoveryPayloadBuilder.BuildConsensus(
            location, _parameters,
            _forecastDays * 24, _forecastDays,
            ctx.Mqtt, ctx.PollInterval, ctx.Version);

    public IReadOnlyList<MqttMessage> ToStateMessages(object result, string baseTopic, string location) =>
        result is ConsensusResult consensus
            ? StatePayloadBuilder.FromConsensus(consensus, baseTopic, location)
            : [];
}
