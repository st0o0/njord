using Njord.Core.AirQuality;
using Njord.Core.Domain;
using Njord.Core.Forecast;

namespace Njord.Core.Tests.Domain;

public sealed class DomainRequirementsExtensionsTests
{
    [Fact]
    public void Should_list_required_domains_in_order()
    {
        var requirements = new DomainRequirements(
            Forecast: ForecastParams.Temperature,
            OptionalAirQuality: AirQualityParams.BirchPollen,
            Ensemble: EnsembleParams.TemperatureSpread);

        Assert.Equal(
            [DomainKind.Forecast, DomainKind.AirQuality, DomainKind.Ensemble],
            requirements.RequiredDomains());
    }

    [Fact]
    public void Should_list_no_domains_for_empty_requirements()
    {
        Assert.Empty(new DomainRequirements().RequiredDomains());
    }
}
