using Njord.Consensus;
using Njord.Model;
using Njord.Mqtt.Mapping;
using Njord.Mqtt.Payloads;

namespace Njord.Mqtt.Tests.Mapping;

public sealed class ConsensusPayloadMappingsSpec
{
    private static readonly WeatherModel IconD2 = new("icon_d2");
    private static readonly WeatherModel Ecmwf = new("ecmwf_ifs025");

    private static HorizonConsensus Consensus(double? median, double? spread, double? agreement, params WeatherModel[] models)
        => new(median, null, spread, null, agreement, null, null, models);

    [Fact]
    public void Entries_follow_parameter_order_and_round_median_spread_and_agreement()
    {
        var h3 = Horizon.Hours(3);
        var parameters = new List<ParameterConsensus>
        {
            new(ParameterRegistry.Temperature2m,
                new Dictionary<Horizon, HorizonConsensus> { [h3] = Consensus(12.3456, 1.23456, 0.87654, IconD2, Ecmwf) }),
            new(ParameterRegistry.WindSpeed10m,
                new Dictionary<Horizon, HorizonConsensus> { [h3] = Consensus(4.5678, 0.5, 0.5, IconD2) }),
        };

        var (horizon, payload) = Assert.Single(parameters.ToPayload());

        Assert.Equal(h3, horizon);
        Assert.Equal(2, payload.Parameters.Count);
        var spread = Assert.IsType<ConsensusSpreadPayload>(payload.Spread);
        Assert.Multiple(
            () => Assert.Equal("temperature", payload.Parameters[0].Key),
            () => Assert.Equal("temperature_models", payload.Parameters[0].ModelsKey),
            () => Assert.Equal(12.35, payload.Parameters[0].Median),
            () => Assert.Equal(2, payload.Parameters[0].Models),
            () => Assert.Equal("wind_speed_10m", payload.Parameters[1].Key),
            () => Assert.Equal("wind_speed_10m_models", payload.Parameters[1].ModelsKey),
            () => Assert.Equal(4.57, payload.Parameters[1].Median),
            () => Assert.Equal(1, payload.Parameters[1].Models),
            () => Assert.Equal(1.23, spread.Spread),
            () => Assert.Equal(0.877, spread.Agreement));
    }

    [Fact]
    public void Missing_median_spread_and_agreement_map_to_null()
    {
        var h6 = Horizon.Hours(6);
        var parameters = new List<ParameterConsensus>
        {
            new(ParameterRegistry.Temperature2m,
                new Dictionary<Horizon, HorizonConsensus> { [h6] = Consensus(null, null, null) }),
        };

        var (_, payload) = Assert.Single(parameters.ToPayload());

        var spread = Assert.IsType<ConsensusSpreadPayload>(payload.Spread);
        var entry = Assert.Single(payload.Parameters);
        Assert.Multiple(
            () => Assert.Null(entry.Median),
            () => Assert.Equal(0, entry.Models),
            () => Assert.Null(spread.Spread),
            () => Assert.Null(spread.Agreement));
    }

    [Fact]
    public void Spread_and_agreement_come_from_the_first_parameter_and_are_absent_when_it_lacks_the_horizon()
    {
        var h3 = Horizon.Hours(3);
        var h6 = Horizon.Hours(6);
        var parameters = new List<ParameterConsensus>
        {
            new(ParameterRegistry.Temperature2m,
                new Dictionary<Horizon, HorizonConsensus> { [h3] = Consensus(1.0, 0.1, 0.9, IconD2) }),
            new(ParameterRegistry.WindSpeed10m,
                new Dictionary<Horizon, HorizonConsensus>
                {
                    [h3] = Consensus(2.0, 0.2, 0.8, IconD2),
                    [h6] = Consensus(3.0, 0.3, 0.7, IconD2),
                }),
        };

        var payloads = parameters.ToPayload();

        Assert.Equal([h3, h6], payloads.Select(p => p.Horizon));
        var first = Assert.IsType<ConsensusSpreadPayload>(payloads[0].Payload.Spread);
        Assert.Multiple(
            () => Assert.Equal(0.1, first.Spread),
            () => Assert.Equal(0.9, first.Agreement),
            () => Assert.Null(payloads[1].Payload.Spread),
            () => Assert.Equal(["wind_speed_10m"], payloads[1].Payload.Parameters.Select(p => p.Key)));
    }

    [Fact]
    public void No_parameters_map_to_no_payloads()
    {
        Assert.Empty(new List<ParameterConsensus>().ToPayload());
    }
}
