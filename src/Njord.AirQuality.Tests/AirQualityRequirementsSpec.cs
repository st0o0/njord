using Njord.Core.Domain;
using Njord.Forecast.AirQuality;
using Njord.Messages.Alerts;
using Njord.Tests.Shared;

namespace Njord.AirQuality.Tests;

public sealed class AirQualityRequirementsSpec
{
    private static AirQualityAlertHandler Create(AlertType type) =>
        new([new PollenAlertRule(), new Pm25AlertRule()], AlertRuleFixture.OptionsWith(true), type);

    [Fact]
    public void The_pm25_alert_requires_pm25_only()
    {
        Assert.Equal(new DomainRequirements(AirQuality: AirQualityParams.Pm25), Create(AlertType.Pm25).Feature.Requirements);
    }

    [Fact]
    public void The_pollen_alert_requires_every_pollen_type()
    {
        Assert.Equal(
            new DomainRequirements(AirQuality: AirQualityParams.BirchPollen | AirQualityParams.GrassPollen
                | AirQualityParams.Alder | AirQualityParams.Mugwort),
            Create(AlertType.Pollen).Feature.Requirements);
    }

    [Fact]
    public void The_pollen_trend_requires_every_pollen_type()
    {
        Assert.Equal(
            new DomainRequirements(AirQuality: AirQualityParams.BirchPollen | AirQualityParams.GrassPollen
                | AirQualityParams.Alder | AirQualityParams.Mugwort),
            new PollenTrendHandler().Feature.Requirements);
    }
}
