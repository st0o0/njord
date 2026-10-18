using Njord.Consensus;
using Njord.Grpc.Mapping;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;
using Njord.Weather;
using DomainAlert = Njord.Messages.Alerts.Alert;
using DomainAlertSeverity = Njord.Messages.Alerts.AlertSeverity;
using DomainAlertType = Njord.Messages.Alerts.AlertType;
using DomainHorizonConsensus = Njord.Consensus.HorizonConsensus;
using DomainHorizonDerived = Njord.Messages.Derived.HorizonDerived;
using DomainParameterConsensus = Njord.Consensus.ParameterConsensus;
using DomainParameterTrend = Njord.Messages.Trends.ParameterTrend;
using DomainScalarDerived = Njord.Messages.Derived.ScalarDerived;

namespace Njord.Grpc.Tests;

public sealed class ConsensusProtoMappingsSpec
{
    [Fact]
    public void Consensus_should_map_parameter_and_horizon_nesting()
    {
        var param = new ParameterDef("temperature_2m", "C", ParameterGroup.Weather, ParameterGranularity.Hourly);

        var horizonConsensus = new DomainHorizonConsensus(
            Median: 20.5,
            TrimmedMean: 20.3,
            Spread: 2.1,
            Iqr: 1.5,
            Agreement: 0.85,
            Outlier: new OutlierInfo(new WeatherModel("gfs_seamless"), 3.2),
            ConfidenceInterval: new ConfidenceIntervalInfo(18.0, 23.0),
            AvailableModels: [new WeatherModel("icon_d2"), new WeatherModel("ecmwf_ifs025")]);

        var paramConsensus = new DomainParameterConsensus(
            param,
            new Dictionary<Horizon, DomainHorizonConsensus> { [Horizon.Hours(3)] = horizonConsensus });

        var result = new ConsensusResult([paramConsensus]);

        var update = ConsensusProtoMappings.ToProto(result);

        Assert.Single(update.HourlyParameters);
        Assert.Equal("temperature_2m", update.HourlyParameters[0].Parameter);
        Assert.Equal("C", update.HourlyParameters[0].Unit);
        Assert.Single(update.HourlyParameters[0].ByHorizon);

        var h = update.HourlyParameters[0].ByHorizon[0];
        Assert.Equal("h3", h.Horizon);
        Assert.Equal(20.5, h.Median);
        Assert.Equal(20.3, h.TrimmedMean);
        Assert.Equal(2.1, h.Spread);
        Assert.Equal(1.5, h.Iqr);
        Assert.Equal(0.85, h.Agreement);
        Assert.Equal(2, h.AvailableModels);
    }

    [Fact]
    public void Consensus_should_map_daily_parameters()
    {
        var tempMax = ParameterRegistry.GetByApiName("temperature_2m_max")!;
        var dailyParams = new List<DomainParameterConsensus>
        {
            new(tempMax, new Dictionary<Horizon, DomainHorizonConsensus>
            {
                [Horizon.Days(0)] = new(29.5, 29.3, 3.0, 1.5, 0.83, null, null,
                    [new WeatherModel("icon_d2"), new WeatherModel("ecmwf_ifs025"), new WeatherModel("gfs_seamless")]),
            }),
        };
        var result = new ConsensusResult([], (IReadOnlyList<DomainParameterConsensus>)dailyParams);

        var update = ConsensusProtoMappings.ToProto(result);

        Assert.Single(update.DailyParameters);
        var proto = update.DailyParameters[0];
        Assert.Equal("temperature_2m_max", proto.Parameter);
        Assert.Single(proto.ByHorizon);
        Assert.Equal("d0", proto.ByHorizon[0].Horizon);
        Assert.Equal(29.5, proto.ByHorizon[0].Median);
        Assert.Equal(3, proto.ByHorizon[0].AvailableModels);
    }

    [Fact]
    public void Consensus_should_produce_empty_daily_for_no_parameters()
    {
        var result = new ConsensusResult([]);

        var update = ConsensusProtoMappings.ToProto(result);

        Assert.Empty(update.DailyParameters);
    }
}
