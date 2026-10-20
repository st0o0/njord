namespace Njord.Forecast.Tests;

public sealed class DomainKindSpec
{
    [Fact]
    public void Should_define_the_six_forecast_domains()
    {
        Assert.Equal(
            ["Weather", "AirQuality", "Flood", "Solar", "Marine", "Seasonal"],
            Enum.GetNames<DomainKind>());
    }
}
