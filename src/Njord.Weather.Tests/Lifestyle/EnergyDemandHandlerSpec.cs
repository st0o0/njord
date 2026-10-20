using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Configuration;
using Njord.Messages.Pipeline;
using Njord.Weather.Lifestyle;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Weather.Tests.Lifestyle.LifestyleFixture;

namespace Njord.Weather.Tests.Lifestyle;

public sealed class EnergyDemandHandlerSpec
{
    private static readonly EnergyDemandHandler Handler = new(Options.Create(new EnergyDemandOptions()));

    private static LocationForecast Constant(double apparent) =>
        WeatherOnly(Hourly(48, (_, point) => point with { ApparentTemperature = apparent }));

    [Fact]
    public void Handle_sums_heating_degree_hours_below_the_setpoint()
    {
        var demand = Assert.IsType<PipelineEvent.EnergyDemandComputed>(Assert.Single(Handler.Handle(Constant(5.0), null, Time)));

        Assert.Multiple(
            () => Assert.Equal(15.0 * 48, demand.HeatingDegreeHours),
            () => Assert.Equal(0.0, demand.CoolingDegreeHours));
    }

    [Fact]
    public void Handle_sums_cooling_degree_hours_above_the_setpoint_and_reports_the_peak_hour()
    {
        var forecast = WeatherOnly(Hourly(48, (hour, point) => point with { ApparentTemperature = hour == 30 ? 36.0 : 26.0 }));

        var demand = Assert.IsType<PipelineEvent.EnergyDemandComputed>(Assert.Single(Handler.Handle(forecast, null, Time)));

        Assert.Multiple(
            () => Assert.Equal(10.0, demand.CoolingDegreeHours),
            () => Assert.Equal(Now.AddHours(30).Hour, demand.PeakDemandHour));
    }

    [Fact]
    public void Handle_respects_configured_setpoints()
    {
        var handler = new EnergyDemandHandler(Options.Create(new EnergyDemandOptions { HeatingSetpoint = 10.0 }));

        var demand = Assert.IsType<PipelineEvent.EnergyDemandComputed>(Assert.Single(handler.Handle(Constant(5.0), null, Time)));

        Assert.Equal(5.0 * 48, demand.HeatingDegreeHours);
    }
}
