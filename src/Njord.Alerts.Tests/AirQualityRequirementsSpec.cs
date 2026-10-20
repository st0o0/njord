using Njord.Core.Domain;
using Njord.Forecast.AirQuality;
using Njord.Tests.Shared;

namespace Njord.Alerts.Tests;

public sealed class AirQualityRequirementsSpec
{
    private static IFeature Rule(string name) =>
        FeatureServices.AirQualityHandler(new Microsoft.Extensions.Time.Testing.FakeTimeProvider(AirQualityFixture.Now))
            .Features.Single(f => f.Name == name);

    [Fact]
    public void The_pm25_alert_requires_pm25_only()
    {
        Assert.Equal(new DomainRequirements(AirQuality: AirQualityParams.Pm25), Rule("Pm25").Feature.Requirements);
    }

    [Fact]
    public void The_pollen_alert_requires_every_pollen_type()
    {
        Assert.Equal(
            new DomainRequirements(AirQuality: AirQualityParams.BirchPollen | AirQualityParams.GrassPollen
                | AirQualityParams.Alder | AirQualityParams.Mugwort),
            Rule("Pollen").Feature.Requirements);
    }

    [Fact]
    public void The_dust_alert_requires_dust_only()
    {
        Assert.Equal(new DomainRequirements(AirQuality: AirQualityParams.Dust), Rule("Dust").Feature.Requirements);
    }
}
