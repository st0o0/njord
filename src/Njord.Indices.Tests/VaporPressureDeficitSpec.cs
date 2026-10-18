using Njord.Messages.Derived;

namespace Njord.Indices.Tests;

public sealed class VaporPressureDeficitSpec
{
    [Fact]
    public void VpdCategory_high()
    {
        var result = VaporPressureDeficit.Categorize(25, 60);
        Assert.NotNull(result);
        Assert.Equal(VpdCategory.High, result.Category);
        Assert.InRange(result.Vpd, 1.2, 1.4);
    }

    [Fact]
    public void VpdCategory_low()
    {
        var result = VaporPressureDeficit.Categorize(20, 90);
        Assert.NotNull(result);
        Assert.Equal(VpdCategory.Low, result.Category);
    }

    [Fact]
    public void VpdCategory_optimal()
    {
        var result = VaporPressureDeficit.Categorize(20, 60);
        Assert.NotNull(result);
        Assert.Equal(VpdCategory.Optimal, result.Category);
        Assert.InRange(result.Vpd, 0.4, 1.2);
    }

    [Fact]
    public void VpdCategory_critical()
    {
        var result = VaporPressureDeficit.Categorize(35, 30);
        Assert.NotNull(result);
        Assert.Equal(VpdCategory.Critical, result.Category);
        Assert.True(result.Vpd >= 2.0);
    }

    [Fact]
    public void VpdCategory_null() =>
        Assert.Null(VaporPressureDeficit.Categorize(null, 60));
}
