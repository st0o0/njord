namespace Njord.Indices.Tests;

public sealed class VaporPressureDeficitSpec
{
    [Fact]
    public void VpdCategory_high()
    {
        var result = VaporPressureDeficit.Categorize(25, 60);
        Assert.NotNull(result);
        Assert.Equal("high", result.Category);
        Assert.InRange(result.Vpd, 1.2, 1.4);
    }

    [Fact]
    public void VpdCategory_low()
    {
        var result = VaporPressureDeficit.Categorize(20, 90);
        Assert.NotNull(result);
        Assert.Equal("low", result.Category);
    }

    [Fact]
    public void VpdCategory_null() =>
        Assert.Null(VaporPressureDeficit.Categorize(null, 60));
}
