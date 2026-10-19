namespace Njord.Model.Tests;

public sealed class DomainKindSpec
{
    [Fact]
    public void Should_define_the_five_forecast_domains()
    {
        Assert.Equal(
            ["Weather", "AirQuality", "Flood", "Solar", "Marine"],
            Enum.GetNames<DomainKind>());
    }
}
