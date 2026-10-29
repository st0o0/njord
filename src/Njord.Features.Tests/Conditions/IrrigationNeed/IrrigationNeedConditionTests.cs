using Microsoft.Extensions.Options;
using Njord.Core.Forecast;
using Njord.Features.Conditions.IrrigationNeed;
using Njord.Messages.Results.Conditions;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Conditions.IrrigationNeed;

public sealed class IrrigationNeedConditionTests
{
    private static IrrigationNeedCondition Feature() => new(Options.Create(new IrrigationNeedConfig { Enabled = true }));

    private static IrrigationNeedConditionResult Evaluate(Func<ForecastPoint, ForecastPoint> shape) =>
        Assert.IsType<IrrigationNeedConditionResult>(Run(Feature(), Now, ForecastOnly(Uniform(shape))));

    [Fact]
    public void Dry_soil_with_high_evapotranspiration_and_no_rain_needs_water()
    {
        var result = Evaluate(p => p with { SoilMoisture0To1cm = 0.15, Evapotranspiration = 0.2, Rain = 0.0 });

        Assert.Multiple(
            () => Assert.Equal(IrrigationAdvice.Water, result.Advice),
            () => Assert.Equal(15.0, result.SoilMoisturePercent),
            () => Assert.Equal(4.8, result.EtMm),
            () => Assert.Equal(0.0, result.RainExpectedMm));
    }

    [Fact]
    public void Adequate_soil_without_rain_skips_watering()
    {
        var result = Evaluate(p => p with { SoilMoisture0To1cm = 0.4, Evapotranspiration = 0.2, Rain = 0.0 });

        Assert.Equal(IrrigationAdvice.Skip, result.Advice);
    }

    [Fact]
    public void Wet_soil_is_reported_as_wet()
    {
        var result = Evaluate(p => p with { SoilMoisture0To1cm = 0.7, Evapotranspiration = 0.1, Rain = 0.0 });

        Assert.Equal(IrrigationAdvice.SoilWet, result.Advice);
    }

    [Fact]
    public void Heavy_likely_rain_skips_watering_even_on_dry_soil()
    {
        var result = Evaluate(p => p with { SoilMoisture0To1cm = 0.1, Evapotranspiration = 0.1, Rain = 0.5, PrecipitationProbability = 90.0 });

        Assert.Multiple(
            () => Assert.Equal(IrrigationAdvice.Skip, result.Advice),
            () => Assert.Equal(10.8, result.RainExpectedMm));
    }

    [Fact]
    public void Some_expected_rain_reduces_watering()
    {
        var result = Evaluate(p => p with { SoilMoisture0To1cm = 0.4, Evapotranspiration = 0.1, Rain = 0.1, PrecipitationProbability = 50.0 });

        Assert.Equal(IrrigationAdvice.Reduce, result.Advice);
    }

    [Fact]
    public void Dry_soil_with_rain_covering_the_evapotranspiration_reduces_watering()
    {
        var result = Evaluate(p => p with { SoilMoisture0To1cm = 0.1, Evapotranspiration = 0.05, Rain = 0.1, PrecipitationProbability = 100.0 });

        Assert.Equal(IrrigationAdvice.Reduce, result.Advice);
    }

    [Fact]
    public void Missing_soil_moisture_skips_the_rule()
    {
        Assert.Null(Run(Feature(), Now, ForecastOnly(Uniform(p => p with { SoilMoisture0To1cm = null }))));
    }
}
