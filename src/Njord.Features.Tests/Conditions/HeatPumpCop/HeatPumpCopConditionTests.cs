using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Conditions.HeatPumpCop;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Conditions.HeatPumpCop;

public sealed class HeatPumpCopConditionTests
{
    private static HeatPumpCopCondition Feature(Action<HeatPumpCopConfig>? configure = null)
    {
        var config = new HeatPumpCopConfig { Enabled = true };
        configure?.Invoke(config);
        return new(Options.Create(config));
    }

    private static HeatPumpCopConditionResult Evaluate(double temperature)
    {
        var forecast = ForecastOnly(Uniform(point => point with { Temperature = temperature }));
        return Assert.IsType<HeatPumpCopConditionResult>(Run(Feature(), Now, forecast));
    }

    [Fact]
    public void Mild_weather_is_ideal()
    {
        var result = Evaluate(15.0);

        Assert.Multiple(
            () => Assert.Equal(Math.Round(6.81 - 0.121 * 20.0, 2), result.Cop),
            () => Assert.Equal(HeatPumpRecommendation.Ideal, result.Recommendation));
    }

    [Fact]
    public void Cold_weather_is_normal()
    {
        Assert.Equal(HeatPumpRecommendation.Normal, Evaluate(0.0).Recommendation);
    }

    [Fact]
    public void Frost_is_inefficient()
    {
        Assert.Equal(HeatPumpRecommendation.Inefficient, Evaluate(-5.0).Recommendation);
    }

    [Fact]
    public void Extreme_cold_clamps_the_cop_to_one_and_is_avoided()
    {
        var result = Evaluate(-60.0);

        Assert.Multiple(
            () => Assert.Equal(1.0, result.Cop),
            () => Assert.Equal(HeatPumpRecommendation.Avoid, result.Recommendation));
    }

    [Theory]
    [InlineData(4.01, HeatPumpRecommendation.Ideal)]
    [InlineData(4.0, HeatPumpRecommendation.Normal)]
    [InlineData(2.5, HeatPumpRecommendation.Normal)]
    [InlineData(2.49, HeatPumpRecommendation.Inefficient)]
    [InlineData(1.5, HeatPumpRecommendation.Inefficient)]
    [InlineData(1.49, HeatPumpRecommendation.Avoid)]
    public void Recommendation_boundaries(double cop, HeatPumpRecommendation expected)
    {
        Assert.Equal(expected, HeatPumpCopCondition.Recommend(cop));
    }

    [Fact]
    public void The_best_hour_is_the_warmest_hour_of_the_window()
    {
        var forecast = ForecastOnly(Day((hour, point) => point with { Temperature = hour == 5 ? 20.0 : 5.0 }));

        var result = Assert.IsType<HeatPumpCopConditionResult>(Run(Feature(), Now, forecast));

        Assert.Multiple(
            () => Assert.Equal(Now.AddHours(5).Hour, result.BestHour),
            () => Assert.Equal(Math.Round(6.81 - 0.121 * 15.0, 2), result.BestCop),
            () => Assert.Equal(Math.Round(6.81 - 0.121 * 30.0, 2), result.Cop));
    }

    [Fact]
    public void Hours_beyond_the_forecast_window_are_ignored()
    {
        var forecast = ForecastOnly(Day((hour, point) => point with { Temperature = hour == 5 ? 20.0 : 5.0 }));

        var result = Assert.IsType<HeatPumpCopConditionResult>(Run(Feature(c => c.ForecastHours = 3), Now, forecast));

        Assert.Equal(Math.Round(6.81 - 0.121 * 30.0, 2), result.BestCop);
    }

    [Fact]
    public void Nothing_is_reported_without_a_temperature()
    {
        var forecast = ForecastOnly(Uniform(point => point with { Temperature = null }));

        Assert.Null(Run(Feature(), Now, forecast));
    }

    [Fact]
    public void The_requirements_ask_for_temperature_and_humidity()
    {
        Assert.Equal(ForecastParams.Temperature | ForecastParams.Humidity, Feature().Descriptor.Requirements.Forecast);
    }
}
