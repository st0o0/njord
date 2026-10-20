using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Scores.Bbq;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Scores;

public sealed class BbqScoreSpec
{
    private static BbqScore Feature(bool enabled = true) => new(Options.Create(new BbqConfig { Enabled = enabled }));

    [Fact]
    public void An_ideal_evening_scores_at_least_ninety()
    {
        var forecast = WeatherOnly(Uniform(point => point with { Temperature = 24.0, WindSpeed = 2.0, CloudCover = 20.0 }));

        var score = Assert.IsType<BbqScoreResult>(Assert.Single(Run(Feature(), Now, forecast)));

        Assert.Multiple(
            () => Assert.True(score.Score >= 90, $"score was {score.Score}"),
            () => Assert.Equal(24, score.BestHours));
    }

    [Fact]
    public void The_dry_window_between_showers_is_found()
    {
        var forecast = WeatherOnly(Day((hour, point) => point with
        {
            Temperature = 24.0,
            WindSpeed = 2.0,
            CloudCover = 20.0,
            Precipitation = hour is < 4 or >= 10 ? 2.0 : 0.0,
        }));

        var score = Assert.IsType<BbqScoreResult>(Assert.Single(Run(Feature(), Now, forecast)));

        Assert.Multiple(
            () => Assert.Equal(Now.AddHours(4), score.BestStart),
            () => Assert.Equal(6, score.BestHours));
    }

    [Fact]
    public void Rain_all_day_reports_no_window()
    {
        var forecast = WeatherOnly(Uniform(point => point with { Precipitation = 3.0 }));

        var score = Assert.IsType<BbqScoreResult>(Assert.Single(Run(Feature(), Now, forecast)));

        Assert.Multiple(
            () => Assert.Equal(0, score.Score),
            () => Assert.Equal(0, score.BestHours));
    }

    [Fact]
    public void Missing_weather_data_yields_nothing()
    {
        Assert.Empty(Run(Feature(), Now, new LocationForecast(Lucerne)));
    }

    [Fact]
    public void The_feature_follows_its_configured_toggle()
    {
        Assert.Multiple(
            () => Assert.False(Feature(false).Enabled),
            () => Assert.True(Feature().Enabled),
            () => Assert.False(Feature().Descriptor.DefaultEnabled));
    }
}
