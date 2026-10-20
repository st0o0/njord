using Njord.Core.Domain;
using Njord.Forecast.Flood;
using Njord.Forecast.Weather;
using Njord.Tests.Shared;

namespace Njord.Alerts.Tests;

public sealed class FloodRequirementsSpec
{
    [Fact]
    public void The_risk_handler_requires_the_discharge_statistics_it_classifies_with()
    {
        var handler = new FloodRiskHandler(new FloodRiskAlertRule(), AlertRuleFixture.OptionsWith(true));

        Assert.Equal(
            new DomainRequirements(Flood: FloodParams.Discharge | FloodParams.DischargeMedian
                | FloodParams.DischargeP75 | FloodParams.DischargeMax),
            handler.Feature.Requirements);
    }

    [Fact]
    public void The_rain_handler_requires_flood_and_precipitation_data()
    {
        var handler = new FloodRainHandler(new FloodRainAlertRule(), AlertRuleFixture.OptionsWith(true));

        Assert.Equal(
            new DomainRequirements(
                Weather: WeatherParams.Precipitation,
                Flood: FloodParams.Discharge | FloodParams.DischargeMedian | FloodParams.DischargeP75),
            handler.Feature.Requirements);
    }

}
