using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Conditions.InversionDetection;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Conditions.InversionDetection;

public sealed class InversionDetectionConditionTests
{
    private static InversionDetectionCondition Feature() => new(Options.Create(new InversionDetectionConfig { Enabled = true }));

    private static PipelineEvent? Eval(double surface, double t80, double? t120, double layer, double cloud, double wind)
    {
        var forecast = ForecastOnly(Uniform(point => point with
        {
            Temperature = surface,
            Temperature80m = t80,
            Temperature120m = t120,
            BoundaryLayerHeight = layer,
            CloudCover = cloud,
            WindSpeed = wind,
        }));
        return Run(Feature(), Now, forecast);
    }

    [Fact]
    public void A_clear_calm_shallow_inversion_is_a_strong_radiation_inversion()
    {
        var result = Assert.IsType<InversionDetectionConditionResult>(Eval(2.0, 4.5, 5.0, 400, 10, 1));

        Assert.Multiple(
            () => Assert.Equal(InversionDetectionCondition.Strong, result.Strength),
            () => Assert.Equal(InversionDetectionCondition.Radiation, result.InversionType),
            () => Assert.Equal(2.5, result.TemperatureDifferenceC),
            () => Assert.Equal(400, result.BoundaryLayerHeightM));
    }

    [Fact]
    public void A_cloudy_windy_strong_shallow_inversion_is_subsidence()
    {
        var result = Assert.IsType<InversionDetectionConditionResult>(Eval(2.0, 5.0, 5.0, 400, 80, 6));

        Assert.Equal(InversionDetectionCondition.Subsidence, result.InversionType);
    }

    [Fact]
    public void A_mild_cloudy_inversion_is_frontal()
    {
        var result = Assert.IsType<InversionDetectionConditionResult>(Eval(2.0, 3.0, 3.0, 700, 80, 6));

        Assert.Multiple(
            () => Assert.Equal(InversionDetectionCondition.Mild, result.Strength),
            () => Assert.Equal(InversionDetectionCondition.Frontal, result.InversionType));
    }

    [Fact]
    public void A_very_shallow_strong_inversion_is_extreme()
    {
        var result = Assert.IsType<InversionDetectionConditionResult>(Eval(0.0, 5.0, 6.0, 200, 10, 1));

        Assert.Equal(InversionDetectionCondition.Extreme, result.Strength);
    }

    [Fact]
    public void A_normal_atmosphere_reports_nothing()
    {
        Assert.Null(Eval(10.0, 9.0, 8.0, 400, 10, 1));
    }

    [Fact]
    public void A_deep_boundary_layer_reports_nothing()
    {
        Assert.Null(Eval(2.0, 5.0, 5.0, 800, 10, 1));
    }

    [Theory]
    [InlineData(0.0, 400, InversionDetectionCondition.None)]
    [InlineData(1.0, 799, InversionDetectionCondition.Mild)]
    [InlineData(1.0, 800, InversionDetectionCondition.None)]
    [InlineData(2.01, 499, InversionDetectionCondition.Strong)]
    [InlineData(2.0, 499, InversionDetectionCondition.Mild)]
    [InlineData(4.01, 299, InversionDetectionCondition.Extreme)]
    [InlineData(4.01, 300, InversionDetectionCondition.Strong)]
    public void Strength_boundaries(double diff, double layer, string expected)
    {
        Assert.Equal(expected, InversionDetectionCondition.ClassifyStrength(diff, layer));
    }

    [Fact]
    public void Nothing_is_reported_without_the_80m_temperature()
    {
        var forecast = ForecastOnly(Uniform(point => point with { Temperature80m = null }));

        Assert.Null(Run(Feature(), Now, forecast));
    }
}
