using Njord.Core.Domain;
using Njord.Forecast.Flood;
using Njord.Forecast.Weather;
using Njord.Tests.Shared;

namespace Njord.Flood.Tests;

public sealed class FloodRequirementsSpec
{
    [Fact]
    public void The_risk_handler_requires_the_discharge_statistics_it_classifies_with()
    {
        var handler = new FloodRiskHandler(new FloodRiskAlertRule(), AlertRuleFixture.OptionsWith(true));

        Assert.Equal(
            new DomainRequirements(Flood: FloodParam.Discharge | FloodParam.DischargeMedian
                | FloodParam.DischargeP75 | FloodParam.DischargeMax),
            handler.Requirements);
    }

    [Fact]
    public void The_rain_handler_requires_flood_and_precipitation_data()
    {
        var handler = new FloodRainHandler(new FloodRainAlertRule(), AlertRuleFixture.OptionsWith(true));

        Assert.Equal(
            new DomainRequirements(
                Weather: WeatherParam.Precipitation,
                Flood: FloodParam.Discharge | FloodParam.DischargeMedian | FloodParam.DischargeP75),
            handler.Requirements);
    }

    [Fact]
    public void The_timeline_handler_requires_discharge_and_the_warning_level()
    {
        Assert.Equal(
            new DomainRequirements(Flood: FloodParam.Discharge | FloodParam.DischargeP75),
            new FloodTimelineHandler().Requirements);
    }
}
