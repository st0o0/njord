using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Trends.ConfidenceTrend;
using Njord.Messages.Results.Trends;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Trends.ConfidenceTrend;

public sealed class ConfidenceTrendTests
{
    private static Features.Trends.ConfidenceTrend.ConfidenceTrend Feature(ConfidenceTrendConfig? cfg = null) =>
        new(Options.Create(cfg ?? new ConfidenceTrendConfig { Enabled = true }));

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
    public void First_cycle_reports_stable_with_zero_magnitude()
    {
        var trend = Assert.IsType<ConfidenceTrendResult>(
            Run(Feature(), Now, WithSpread(2.0)));

        Assert.Multiple(
            () => Assert.Equal(ConfidenceTrendDirection.Stable, trend.Direction),
            () => Assert.Equal(0.0, trend.Magnitude),
            () => Assert.Equal(2.0, trend.CurrentAvgStdDev),
            () => Assert.Equal(2.0, trend.PreviousAvgStdDev));
    }

    [Fact]
    public void Nothing_is_reported_without_ensemble_data() => Assert.Null(Run(Feature(), Now, new LocationForecast(Lucerne), new LocationForecast(Lucerne)));

    [Fact]
    public void Nothing_is_reported_without_spread_data_on_first_cycle() => Assert.Null(Run(Feature(), Now, new LocationForecast(Lucerne)));

    [Fact]
    public void Lower_threshold_detects_smaller_shifts()
    {
        var trend = Assert.IsType<ConfidenceTrendResult>(Run(Feature(), Now, WithSpread(2.0), WithSpread(2.3)));
        Assert.Equal(ConfidenceTrendDirection.Stable, trend.Direction);

        var sensitive = Feature(new ConfidenceTrendConfig { Enabled = true, ChangeThreshold = 0.10 });
        var customTrend = Assert.IsType<ConfidenceTrendResult>(Run(sensitive, Now, WithSpread(2.0), WithSpread(2.3)));
        Assert.Equal(ConfidenceTrendDirection.Diverging, customTrend.Direction);
    }
}
