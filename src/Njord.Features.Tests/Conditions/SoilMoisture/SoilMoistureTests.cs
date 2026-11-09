using Microsoft.Extensions.Options;
using Njord.Features.Conditions.SoilMoisture;
using Njord.Messages.Results.Conditions;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Conditions.SoilMoisture;

public sealed class SoilMoistureTests
{
    private static Features.Conditions.SoilMoisture.SoilMoisture Rule(
        double dry = 0.15, double wet = 0.35) =>
        new(Options.Create(new SoilMoistureConfig { Enabled = true, DryThreshold = dry, WetThreshold = wet }));

    [Fact]
    public void Returns_dry_when_moisture_below_threshold()
    {
        var weather = Uniform(p => p with { SoilMoisture0To1Cm = 0.10 });
        var result = Run(Rule(), Now, ForecastOnly(weather));
        var condition = Assert.IsType<SoilMoistureResult>(result);
        Assert.Multiple(
            () => Assert.Equal(SoilMoistureLevel.Dry, condition.Level),
            () => Assert.Equal(0.1, condition.Moisture));
    }

    [Fact]
    public void Returns_optimal_when_moisture_between_thresholds()
    {
        var weather = Uniform(p => p with { SoilMoisture0To1Cm = 0.25 });
        var result = Run(Rule(), Now, ForecastOnly(weather));
        var condition = Assert.IsType<SoilMoistureResult>(result);
        Assert.Equal(SoilMoistureLevel.Optimal, condition.Level);
    }

    [Fact]
    public void Returns_wet_when_moisture_above_threshold()
    {
        var weather = Uniform(p => p with { SoilMoisture0To1Cm = 0.40 });
        var result = Run(Rule(), Now, ForecastOnly(weather));
        var condition = Assert.IsType<SoilMoistureResult>(result);
        Assert.Multiple(
            () => Assert.Equal(SoilMoistureLevel.Wet, condition.Level),
            () => Assert.Equal(0.4, condition.Moisture));
    }

    [Fact]
    public void Returns_null_when_soil_moisture_data_missing()
    {
        var weather = Uniform(p => p with { SoilMoisture0To1Cm = null });
        var result = Run(Rule(), Now, ForecastOnly(weather));
        Assert.Null(result);
    }
}
