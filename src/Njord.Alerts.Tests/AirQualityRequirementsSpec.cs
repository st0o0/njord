using Njord.Core.Domain;
using Njord.Forecast.AirQuality;
using Njord.Messages.Alerts;
using Njord.Tests.Shared;

namespace Njord.Alerts.Tests;

public sealed class AirQualityRequirementsSpec
{
    private static AirQualityAlertHandler Create(string name) =>
        new([new PollenAlertRule(), new Pm25AlertRule(), new DustAlertRule()], AlertRuleFixture.OptionsWith(true), name);

    [Fact]
    public void The_pm25_alert_requires_pm25_only()
    {
        Assert.Equal(new DomainRequirements(AirQuality: AirQualityParams.Pm25), Create("Pm25").Feature.Requirements);
    }

    [Fact]
    public void The_pollen_alert_requires_every_pollen_type()
    {
        Assert.Equal(
            new DomainRequirements(AirQuality: AirQualityParams.BirchPollen | AirQualityParams.GrassPollen
                | AirQualityParams.Alder | AirQualityParams.Mugwort),
            Create("Pollen").Feature.Requirements);
    }

    [Fact]
    public void The_dust_alert_requires_dust_only()
    {
        Assert.Equal(new DomainRequirements(AirQuality: AirQualityParams.Dust), Create("Dust").Feature.Requirements);
    }
}
