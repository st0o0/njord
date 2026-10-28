using Microsoft.Extensions.Options;
using Njord.Core.Forecast;
using Njord.Features.Conditions.FrostDepth;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Conditions.FrostDepth;

public sealed class FrostDepthConditionTests
{
    private static FrostDepthCondition Feature(double pipeDepthCm = 60.0) =>
        new(Options.Create(new FrostDepthConfig { Enabled = true, PipeDepthCm = pipeDepthCm }));

    private static FrostDepthConditionResult Evaluate(Func<ForecastPoint, ForecastPoint> shape, double pipeDepthCm = 60.0) =>
        Assert.IsType<FrostDepthConditionResult>(Run(Feature(pipeDepthCm), Now, ForecastOnly(Hourly(48, (_, p) => shape(p)))));

    [Fact]
    public void Mild_weather_has_no_frost_depth()
    {
        var result = Evaluate(p => p with { Temperature = 5.0, SoilTemperature = 6.0, SnowDepth = 0.0 });

        Assert.Multiple(
            () => Assert.Equal(0.0, result.DepthCm),
            () => Assert.Equal(FrostDepthCondition.None, result.Risk),
            () => Assert.Equal(6.0, result.SoilTemperatureC));
    }

    [Fact]
    public void Light_frost_reaches_the_surface_only()
    {
        var result = Evaluate(p => p with { Temperature = -0.05, SoilTemperature = 1.0, SnowDepth = 0.0, WindSpeed = 2.0 });

        Assert.Multiple(
            () => Assert.Equal(Math.Round(2.4 * Math.Sqrt(2.4), 1), result.DepthCm),
            () => Assert.Equal(FrostDepthCondition.Surface, result.Risk));
    }

    [Fact]
    public void Hard_frost_reaches_a_moderate_depth()
    {
        var result = Evaluate(p => p with { Temperature = -3.0, SoilTemperature = -1.0, SnowDepth = 0.0, WindSpeed = 2.0 });

        Assert.Multiple(
            () => Assert.Equal(Math.Round(2.4 * Math.Sqrt(144.0), 1), result.DepthCm),
            () => Assert.Equal(FrostDepthCondition.Moderate, result.Risk));
    }

    [Fact]
    public void Snow_insulates_the_ground()
    {
        var bare = Evaluate(p => p with { Temperature = -3.0, SoilTemperature = -1.0, SnowDepth = 0.0, WindSpeed = 2.0 });
        var covered = Evaluate(p => p with { Temperature = -3.0, SoilTemperature = -1.0, SnowDepth = 0.2, WindSpeed = 2.0 });

        Assert.Multiple(
            () => Assert.Equal(20.0, covered.SnowInsulationCm),
            () => Assert.Equal(Math.Round(bare.DepthCm / 3.0, 1), covered.DepthCm));
    }

    [Fact]
    public void Wind_amplification_is_capped()
    {
        var calm = FrostDepthCondition.DepthCm(100.0, 0.0, 5.0);
        var gale = FrostDepthCondition.DepthCm(100.0, 0.0, 50.0);

        Assert.Multiple(
            () => Assert.Equal(24.0, calm),
            () => Assert.Equal(36.0, gale, 6));
    }

    [Fact]
    public void A_frost_approaching_the_pipe_depth_is_deep()
    {
        var result = Evaluate(p => p with { Temperature = -3.0, SoilTemperature = -1.0, SnowDepth = 0.0, WindSpeed = 2.0 }, pipeDepthCm: 35.0);

        Assert.Equal(FrostDepthCondition.Deep, result.Risk);
    }

    [Theory]
    [InlineData(0.0, FrostDepthCondition.None)]
    [InlineData(4.9, FrostDepthCondition.Surface)]
    [InlineData(5.0, FrostDepthCondition.Shallow)]
    [InlineData(14.9, FrostDepthCondition.Shallow)]
    [InlineData(15.0, FrostDepthCondition.Moderate)]
    [InlineData(29.9, FrostDepthCondition.Moderate)]
    [InlineData(30.0, FrostDepthCondition.Deep)]
    public void Risk_boundaries(double depth, string expected)
    {
        Assert.Equal(expected, FrostDepthCondition.RiskFor(depth, 200.0));
    }
}
