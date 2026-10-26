using Njord.Features.Conditions.Shared;
using Njord.Forecast;
using Njord.Forecast.Marine;
using Njord.Forecast.Weather;
using static Njord.Tests.Shared.MarineFixtures;

namespace Njord.Features.Tests.Conditions;

public sealed class SurfConditionsRuleTests
{
    private static MarinePoint Swell(
        double height, double period, double? direction = 270.0, double? windWaveHeight = null, int hour = 0) =>
        new(Now.AddHours(hour), SwellWaveHeight: height, SwellWavePeriod: period, SwellWaveDirection: direction,
            WindWaveHeight: windWaveHeight);

    [Fact]
    public void Long_period_moderate_swell_with_offshore_wind_is_excellent()
    {
        var result = SurfConditionsRule.Assess(Swell(1.8, 14.0), windDirection: 90.0);

        Assert.NotNull(result);
        Assert.Multiple(
            () => Assert.Equal(100, result.Score),
            () => Assert.Equal("excellent", result.Rating),
            () => Assert.Equal(SurfConditionsRule.Offshore, result.WindRelation));
    }

    [Theory]
    [InlineData(6.0, 10)]
    [InlineData(7.9, 10)]
    [InlineData(8.0, 30)]
    [InlineData(12.0, 30)]
    [InlineData(12.1, 50)]
    public void Swell_period_bands_decide_the_period_share_without_wind(double period, int periodPoints)
    {
        var result = SurfConditionsRule.Assess(Swell(1.5, period), windDirection: null);

        Assert.NotNull(result);
        var expected = (int)Math.Round((periodPoints + 30.0) / 80.0 * 100.0);
        Assert.Equal(period < 8.0 ? Math.Min(expected, SurfConditionsRule.PoorScoreCap) : expected, result.Score);
    }

    [Theory]
    [InlineData(0.4, 5.0)]
    [InlineData(0.5, 20.0)]
    [InlineData(0.99, 20.0)]
    [InlineData(1.0, 30.0)]
    [InlineData(2.5, 30.0)]
    [InlineData(2.6, 20.0)]
    [InlineData(3.5, 20.0)]
    [InlineData(3.6, 5.0)]
    public void Swell_height_band_decides_the_height_share(double height, double heightPoints)
    {
        var result = SurfConditionsRule.Assess(Swell(height, 14.0), windDirection: null);

        Assert.NotNull(result);
        Assert.Equal((int)Math.Round((50.0 + heightPoints) / 80.0 * 100.0), result.Score);
    }

    [Theory]
    [InlineData(90.0, 270.0, "offshore")]
    [InlineData(135.0, 270.0, "offshore")]
    [InlineData(136.0, 270.0, "cross")]
    [InlineData(180.0, 270.0, "cross")]
    [InlineData(225.0, 270.0, "onshore")]
    [InlineData(225.1, 270.0, "onshore")]
    [InlineData(270.0, 270.0, "onshore")]
    [InlineData(350.0, 170.0, "offshore")]
    [InlineData(10.0, 350.0, "onshore")]
    public void Relation_handles_the_angular_wrap_around(double windFrom, double swellFrom, string expected) =>
        Assert.Equal(expected, SurfConditionsRule.Relation(windFrom, swellFrom));

    [Fact]
    public void Onshore_wind_scores_lower_than_cross_wind_than_offshore_wind()
    {
        var offshore = SurfConditionsRule.Assess(Swell(1.8, 10.0), 90.0);
        var cross = SurfConditionsRule.Assess(Swell(1.8, 10.0), 180.0);
        var onshore = SurfConditionsRule.Assess(Swell(1.8, 10.0), 270.0);

        Assert.NotNull(offshore);
        Assert.NotNull(cross);
        Assert.NotNull(onshore);
        Assert.Multiple(
            () => Assert.Equal(80, offshore.Score),
            () => Assert.Equal(70, cross.Score),
            () => Assert.Equal(60, onshore.Score));
    }

    [Fact]
    public void Short_period_wind_chop_is_poor_even_with_offshore_wind()
    {
        var result = SurfConditionsRule.Assess(Swell(1.5, 5.0, windWaveHeight: 1.4), windDirection: 90.0);

        Assert.NotNull(result);
        Assert.Multiple(
            () => Assert.Equal("poor", result.Rating),
            () => Assert.True(result.Score <= SurfConditionsRule.PoorScoreCap));
    }

    [Theory]
    [InlineData(0.5, 0)]
    [InlineData(0.9, 10)]
    [InlineData(1.3, 20)]
    public void A_high_wind_wave_share_lowers_the_score(double windWaveHeight, int penalty)
    {
        var calm = SurfConditionsRule.Assess(Swell(1.6, 10.0), 90.0);
        var choppy = SurfConditionsRule.Assess(Swell(1.6, 10.0, windWaveHeight: windWaveHeight), 90.0);

        Assert.NotNull(calm);
        Assert.NotNull(choppy);
        Assert.Equal(calm.Score - penalty, choppy.Score);
    }

    [Fact]
    public void Missing_wind_is_reported_as_swell_only()
    {
        var result = SurfConditionsRule.Assess(Swell(1.8, 14.0), windDirection: null);

        Assert.NotNull(result);
        Assert.Multiple(
            () => Assert.Null(result.WindRelation),
            () => Assert.Equal(100, result.Score));
    }

    [Fact]
    public void Missing_swell_direction_leaves_wind_out()
    {
        var result = SurfConditionsRule.Assess(Swell(1.8, 14.0, direction: null), windDirection: 90.0);

        Assert.NotNull(result);
        Assert.Null(result.WindRelation);
    }

    [Fact]
    public void Missing_swell_height_or_period_gives_no_assessment()
    {
        Assert.Multiple(
            () => Assert.Null(SurfConditionsRule.Assess(new MarinePoint(Now, SwellWavePeriod: 12.0), null)),
            () => Assert.Null(SurfConditionsRule.Assess(new MarinePoint(Now, SwellWaveHeight: 1.5), null)),
            () => Assert.Null(SurfConditionsRule.Assess(new MarinePoint(Now, WaveHeight: 1.5), null)));
    }

    [Theory]
    [InlineData(0, "poor")]
    [InlineData(39, "poor")]
    [InlineData(40, "fair")]
    [InlineData(59, "fair")]
    [InlineData(60, "good")]
    [InlineData(79, "good")]
    [InlineData(80, "excellent")]
    [InlineData(100, "excellent")]
    public void Rating_follows_the_score_bands(int score, string expected) =>
        Assert.Equal(expected, SurfConditionsRule.RatingFor(score));

    [Fact]
    public void Evaluate_picks_the_best_hour_of_the_window_and_reads_wind_at_that_hour()
    {
        var marine = MarineForecast(Swell(1.8, 6.0, hour: 0), Swell(1.8, 14.0, hour: 1), Swell(1.8, 10.0, hour: 2));
        var weather = new DomainForecast<WeatherPoint>(DomainKind.Weather, Now, TimeResolution.Hourly,
            [new WeatherPoint(Now.AddHours(1), WindDirection: 90.0)]);

        var result = SurfConditionsRule.Evaluate(marine, weather, Now);

        Assert.NotNull(result);
        Assert.Multiple(
            () => Assert.Equal(Now.AddHours(1), result.ValidFrom),
            () => Assert.Equal(100, result.Score),
            () => Assert.Equal(SurfConditionsRule.Offshore, result.WindRelation));
    }

    [Fact]
    public void Evaluate_ignores_hours_before_now_and_beyond_the_window()
    {
        var marine = MarineForecast(Swell(1.8, 14.0, hour: -3), Swell(1.8, 6.0, hour: 0), Swell(1.8, 14.0, hour: 30));

        var result = SurfConditionsRule.Evaluate(marine, null, Now);

        Assert.NotNull(result);
        Assert.Equal(Now, result.ValidFrom);
    }

    [Fact]
    public void Evaluate_returns_nothing_without_swell_data()
    {
        Assert.Null(SurfConditionsRule.Evaluate(MarineForecast(Point(0)), null, Now));
    }
}
