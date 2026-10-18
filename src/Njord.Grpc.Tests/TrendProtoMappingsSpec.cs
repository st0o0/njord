using Njord.Consensus;
using Njord.Grpc.Mapping;
using Njord.Messages.Pipeline;
using Njord.Messages.Trends;
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

public sealed class TrendProtoMappingsSpec
{
    [Fact]
    public void Trends_should_map_parameter_trends_and_nullable_timing()
    {
        var trends = new Dictionary<string, DomainParameterTrend?>
        {
            ["temperature_2m"] = new DomainParameterTrend(TrendDirection.Rising, 1.5),
            ["wind_speed_10m"] = null,
        };
        var result = new TrendResult(
            Location: new LocationId("lucerne"),
            ParameterTrends: trends,
            WeatherChange: new WeatherChangeResult(WeatherCategory.Clear, WeatherCategory.Rain),
            PrecipTiming: new PrecipTimingInfo(StartsInHours: 3, EndsInHours: 8),
            ExtremaTiming: new ExtremaTimingInfo(MaxInHours: 6, MinInHours: 18),
            Stability: new StabilityInfo(Stability: Stability.Stable, Ratio: 0.9),
            Decay: new DecayInfo(DecayRate: 0.15, ReliableHours: 24));

        var update = TrendProtoMappings.ToProto(result);

        Assert.Single(update.ParameterTrends);
        Assert.Equal("temperature_2m", update.ParameterTrends[0].Parameter);
        Assert.Equal(Njord.Grpc.V2.TrendDirection.Rising, update.ParameterTrends[0].Direction);
        Assert.Equal(1.5, update.ParameterTrends[0].Delta);
        Assert.Equal("clear → rain", update.WeatherChangeDescription);
        Assert.Equal(3, update.PrecipStartsInHours);
        Assert.Equal(8, update.PrecipEndsInHours);
        Assert.Equal(6, update.TempMaxInHours);
        Assert.Equal(18, update.TempMinInHours);
        Assert.Equal(Njord.Grpc.V2.Stability.Stable, update.StabilityLabel);
        Assert.Equal(0.9, update.StabilityRatio);
        Assert.Equal(0.15, update.DecayRate);
        Assert.Equal(24, update.ReliableHours);
    }
}
