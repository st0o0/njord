using Njord.Analysis;
using Njord.Domain.Weather;
using Njord.Mqtt;
using static Njord.Mqtt.Tests.EnrichmentGoldenMasterFixtures;
using static VerifyXunit.Verifier;

namespace Njord.Mqtt.Tests;

public sealed class ConsensusSnapshotSpec
{
    private static readonly ResolvedParameterSet SmallParameters = new(
        [EnrichmentGoldenMasterFixtures.Temperature, EnrichmentGoldenMasterFixtures.WindSpeed],
        [ParameterRegistry.GetByApiName("temperature_2m_max")!, ParameterRegistry.GetByApiName("sunrise")!]);

    [Fact]
    public Task Consensus_discovery_payload_matches_golden_master()
    {
        var options = Options();
        var deviceId = TopicScheme.EnrichmentDeviceId(Location, "consensus");
        var configTopic = TopicScheme.ConfigTopic(options.Mqtt.DiscoveryPrefix, deviceId);
        var payload = DiscoveryPayloadBuilder.BuildConsensus(
            Location, SmallParameters, 6, 2,
            options.Mqtt, options.PollInterval, EnrichmentGoldenMasterFixtures.Version);

        return Verify($"config_topic: {configTopic}\n{RenderDiscovery(deviceId, payload)}");
    }

    [Fact]
    public void Consensus_presenter_discovery_wraps_the_builder_byte_identically()
    {
        var options = Options();
        var presenter = Presenters(options, SmallParameters).Single(p => p.TypeName == "consensus");

        var payload = presenter.BuildDiscoveryPayload(Context(options), Location);

        Assert.Equal("njord_lucerne_consensus", presenter.DeviceId(Location));
        Assert.Equal(
            DiscoveryPayloadBuilder.BuildConsensus(
                Location, SmallParameters, options.ForecastDays * 24, options.ForecastDays,
                options.Mqtt, options.PollInterval, EnrichmentGoldenMasterFixtures.Version),
            payload);
    }

    [Fact]
    public void Consensus_presenter_returns_no_messages_for_a_foreign_result()
    {
        var presenter = Presenter("consensus");

        Assert.Empty(presenter.ToStateMessages(new object(), BaseTopic, Location));
    }

    [Fact]
    public Task Consensus_state_messages_match_golden_master()
    {
        var consensus = FullConsensus();
        var result = new ConsensusResult(consensus.Hourly.Parameters, consensus.Daily.Parameters);

        return Verify(Render(Presenter("consensus").ToStateMessages(result, BaseTopic, Location)));
    }
}
