using Njord.Messages.Pipeline;
using Njord.Weather.Lifestyle;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Weather.Tests.Lifestyle.LifestyleFixture;

namespace Njord.Weather.Tests.Lifestyle;

public sealed class LaundryScoreHandlerSpec
{
    private static readonly LaundryScoreHandler Handler = new();

    [Fact]
    public void Handle_scores_a_warm_dry_breezy_sunny_day_at_100_for_all_day()
    {
        var weather = Uniform(p => p with { Temperature = 25.0, Humidity = 40.0, WindSpeed = 5.0, CloudCover = 0.0 });

        var events = Handler.Handle(WeatherOnly(weather), null, Time);

        var score = Assert.IsType<PipelineEvent.LaundryScoreComputed>(Assert.Single(events));
        Assert.Multiple(
            () => Assert.Equal(100, score.Score),
            () => Assert.Equal(Now, score.BestStart),
            () => Assert.Equal(24, score.BestHours));
    }

    [Fact]
    public void Handle_finds_the_dry_stretch_after_rainy_hours()
    {
        var weather = Day((hour, p) => (hour < 6 ? p with { Precipitation = 2.0 } : p) with
        {
            Temperature = 25.0,
            Humidity = 40.0,
            WindSpeed = 5.0,
            CloudCover = 0.0,
        });

        var events = Handler.Handle(WeatherOnly(weather), null, Time);

        var score = Assert.IsType<PipelineEvent.LaundryScoreComputed>(Assert.Single(events));
        Assert.Multiple(
            () => Assert.Equal(Now.AddHours(6), score.BestStart),
            () => Assert.Equal(18, score.BestHours));
    }

    [Fact]
    public void Handle_reports_zero_good_hours_when_it_rains_all_day()
    {
        var weather = Uniform(p => p with { Precipitation = 1.0 });

        var events = Handler.Handle(WeatherOnly(weather), null, Time);

        var score = Assert.IsType<PipelineEvent.LaundryScoreComputed>(Assert.Single(events));
        Assert.Multiple(
            () => Assert.Equal(0, score.Score),
            () => Assert.Equal(0, score.BestHours));
    }
}
