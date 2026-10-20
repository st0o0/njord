using Njord.Core.Domain;
using Njord.Messages.Pipeline;
using Njord.Weather.Lifestyle;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Weather.Tests.Lifestyle.LifestyleFixture;

namespace Njord.Weather.Tests.Lifestyle;

public sealed class ConfidenceTrendHandlerSpec
{
    private static readonly ConfidenceTrendHandler Handler = new();

    private static LocationForecast WithStdDev(double value) =>
        WeatherOnly(Uniform(p => p with { EnsembleStdDev = value }));

    [Theory]
    [InlineData(1.0, 2.0, "Converging")]
    [InlineData(2.5, 2.0, "Diverging")]
    [InlineData(2.1, 2.0, "Stable")]
    public void Handle_classifies_the_change_in_ensemble_spread(double current, double previous, string direction)
    {
        var events = Handler.Handle(WithStdDev(current), WithStdDev(previous), Time);

        var trend = Assert.IsType<PipelineEvent.ConfidenceTrend>(Assert.Single(events));
        Assert.Multiple(
            () => Assert.Equal(direction, trend.Direction),
            () => Assert.Equal(current, trend.CurrentAvgStdDev),
            () => Assert.Equal(previous, trend.PreviousAvgStdDev));
    }

    [Fact]
    public void Handle_returns_nothing_without_a_previous_cycle()
    {
        Assert.Empty(Handler.Handle(WithStdDev(1.0), null, Time));
    }

    [Fact]
    public void Handle_returns_nothing_without_ensemble_data()
    {
        Assert.Empty(Handler.Handle(WeatherOnly(Day()), WithStdDev(1.0), Time));
    }
}
