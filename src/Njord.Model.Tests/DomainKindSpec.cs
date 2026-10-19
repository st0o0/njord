namespace Njord.Model.Tests;

public sealed class DomainKindSpec
{
    [Fact]
    public void Defines_exactly_the_five_domains()
    {
        Assert.Equal(
            [DomainKind.Weather, DomainKind.AirQuality, DomainKind.Flood, DomainKind.Solar, DomainKind.Marine],
            Enum.GetValues<DomainKind>());
    }
}
