using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Conditions.AtmosphericStability;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Conditions.AtmosphericStability;

public sealed class AtmosphericStabilityConditionTests
{
    private static AtmosphericStabilityCondition Feature() => new(Options.Create(new AtmosphericStabilityConfig { Enabled = true }));

    private static PipelineEvent? Eval(double? cape, double cin, double lifted)
    {
        var forecast = ForecastOnly(Uniform(point => point with { Cape = cape, ConvectiveInhibition = cin, LiftedIndex = lifted }));
        return Run(Feature(), Now, forecast);
    }

    [Fact]
    public void The_result_carries_the_raw_values_and_the_class()
    {
        var result = Assert.IsType<AtmosphericStabilityConditionResult>(Eval(2800, 20, -7));

        Assert.Multiple(
            () => Assert.Equal(AtmosphericStabilityCondition.Explosive, result.StabilityClass),
            () => Assert.Equal(2800, result.CapeJPerKg),
            () => Assert.Equal(20, result.CinJPerKg),
            () => Assert.Equal(-7, result.LiftedIndex));
    }

    [Theory]
    [InlineData(299, 0, 0, AtmosphericStabilityCondition.Stable)]
    [InlineData(300, 0, -5, AtmosphericStabilityCondition.Marginal)]
    [InlineData(999, 0, -5, AtmosphericStabilityCondition.Marginal)]
    [InlineData(1000, 201, -5, AtmosphericStabilityCondition.Capped)]
    [InlineData(1000, 200, -5, AtmosphericStabilityCondition.Marginal)]
    [InlineData(1000, 50, -5, AtmosphericStabilityCondition.Marginal)]
    [InlineData(1000, 49, -3.5, AtmosphericStabilityCondition.Unstable)]
    [InlineData(1000, 49, -3, AtmosphericStabilityCondition.Marginal)]
    [InlineData(2500, 49, -6.5, AtmosphericStabilityCondition.Explosive)]
    [InlineData(2500, 49, -6, AtmosphericStabilityCondition.Unstable)]
    [InlineData(2499, 49, -8, AtmosphericStabilityCondition.Unstable)]
    public void Class_boundaries(double cape, double cin, double lifted, string expected)
    {
        Assert.Equal(expected, AtmosphericStabilityCondition.Classify(cape, cin, lifted));
    }

    [Fact]
    public void Nothing_is_reported_without_cape()
    {
        Assert.Null(Eval(null, 0, 0));
    }
}
