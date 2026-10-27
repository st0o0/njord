using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Trends.ConfidenceTrend;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Trends;

public sealed class ConfidenceTrendTests
{
    private static ConfidenceTrend Feature() => new(Options.Create(new ConfidenceTrendConfig { Enabled = true }));

    private static LocationForecast WithSpread(double value) =>
        new(Lucerne, Ensemble: EnsembleDay(value));

    [Theory]
    [InlineData(1.0, 2.0, ConfidenceTrendDirection.Converging)]
    [InlineData(2.5, 2.0, ConfidenceTrendDirection.Diverging)]
    [InlineData(2.1, 2.0, ConfidenceTrendDirection.Stable)]
    public void The_change_in_ensemble_spread_is_classified(double current, double previous, ConfidenceTrendDirection direction)
    {
        var trend = Assert.IsType<ConfidenceTrendResult>(
            Run(Feature(), Now, WithSpread(previous), WithSpread(current)));

        Assert.Multiple(
            () => Assert.Equal(direction, trend.Direction),
            () => Assert.Equal(Math.Round(current - previous, 2), trend.Magnitude),
            () => Assert.Equal(current, trend.CurrentAvgStdDev),
            () => Assert.Equal(previous, trend.PreviousAvgStdDev));
    }

    [Fact]
    public void Nothing_is_reported_without_a_previous_cycle()
    {
        Assert.Null(Run(Feature(), Now, WithSpread(1.0)));
    }

    [Fact]
    public void Nothing_is_reported_without_ensemble_data()
    {
        Assert.Null(Run(Feature(), Now, new LocationForecast(Lucerne), new LocationForecast(Lucerne)));
    }
}
