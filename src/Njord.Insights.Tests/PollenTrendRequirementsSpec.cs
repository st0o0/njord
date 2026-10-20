using Njord.Core.Domain;
using Njord.Forecast.AirQuality;

namespace Njord.Insights.Tests;

public sealed class PollenTrendRequirementsSpec
{
    [Fact]
    public void The_pollen_trend_requires_every_pollen_type()
    {
        Assert.Equal(
            new DomainRequirements(AirQuality: AirQualityParams.BirchPollen | AirQualityParams.GrassPollen
                | AirQualityParams.Alder | AirQualityParams.Mugwort),
            new PollenTrendHandler().Feature.Requirements);
    }
}
