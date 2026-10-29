using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Conditions.CloudCeiling;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Conditions.CloudCeiling;

public sealed class CloudCeilingConditionTests
{
    private static CloudCeilingCondition Feature() => new(Options.Create(new CloudCeilingConfig { Enabled = true }));

    private static PipelineEvent? Eval(double low, double mid, double high, double? layer = 900, double? freezing = 3200)
    {
        var forecast = ForecastOnly(Uniform(point => point with
        {
            CloudCoverLow = low,
            CloudCoverMid = mid,
            CloudCoverHigh = high,
            BoundaryLayerHeight = layer,
            FreezingLevelHeight = freezing,
        }));
        return Run(Feature(), Now, forecast);
    }

    [Fact]
    public void Low_cloud_puts_the_ceiling_at_the_boundary_layer()
    {
        var result = Assert.IsType<CloudCeilingConditionResult>(Eval(80, 90, 90));

        Assert.Multiple(
            () => Assert.Equal(CeilingCategory.Low, result.CeilingCategory),
            () => Assert.Equal(900, result.EstimatedCeilingM),
            () => Assert.Equal(80, result.CloudCoverLowPercent),
            () => Assert.Equal(90, result.CloudCoverMidPercent),
            () => Assert.Equal(90, result.CloudCoverHighPercent));
    }

    [Fact]
    public void Mid_cloud_puts_the_ceiling_at_the_freezing_level()
    {
        var result = Assert.IsType<CloudCeilingConditionResult>(Eval(10, 70, 0));

        Assert.Multiple(
            () => Assert.Equal(CeilingCategory.High, result.CeilingCategory),
            () => Assert.Equal(3200, result.EstimatedCeilingM));
    }

    [Fact]
    public void High_cloud_puts_the_ceiling_at_8000_metres()
    {
        var result = Assert.IsType<CloudCeilingConditionResult>(Eval(10, 10, 70));

        Assert.Multiple(
            () => Assert.Equal(CeilingCategory.VeryHigh, result.CeilingCategory),
            () => Assert.Equal(8000, result.EstimatedCeilingM));
    }

    [Fact]
    public void Clear_sky_is_unlimited()
    {
        var result = Assert.IsType<CloudCeilingConditionResult>(Eval(10, 20, 50));

        Assert.Multiple(
            () => Assert.Equal(CeilingCategory.Unlimited, result.CeilingCategory),
            () => Assert.Equal(0, result.EstimatedCeilingM));
    }

    [Fact]
    public void Low_cloud_without_a_boundary_layer_height_reports_nothing()
    {
        Assert.Null(Eval(80, 0, 0, layer: null));
    }

    [Theory]
    [InlineData(299, CeilingCategory.VeryLow)]
    [InlineData(300, CeilingCategory.Low)]
    [InlineData(999, CeilingCategory.Low)]
    [InlineData(1000, CeilingCategory.Moderate)]
    [InlineData(2999, CeilingCategory.Moderate)]
    [InlineData(3000, CeilingCategory.High)]
    [InlineData(6000, CeilingCategory.High)]
    [InlineData(6001, CeilingCategory.VeryHigh)]
    public void Category_boundaries(double meters, CeilingCategory expected)
    {
        Assert.Equal(expected, CloudCeilingCondition.Categorize(meters));
    }
}
