using Njord.Domain.Weather;
using Njord.Domain.Analysis;

namespace Njord.Tests.Domain.Analysis;

public sealed class IndexScorerSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
    private static readonly ResolvedPreferences Prefs = ResolvedPreferences.Default;

    // --- LaundryDrying ---

    [Fact(Timeout = 5000)]
    public void LaundryDrying_perfect_day() =>
        Assert.InRange(IndexScorer.LaundryDrying(28, 35, 5, 0, 100, Prefs), 90, 100);

    [Fact(Timeout = 5000)]
    public void LaundryDrying_cold_rainy_day() =>
        Assert.InRange(IndexScorer.LaundryDrying(5, 90, 1, 80, 0, Prefs), 0, 15);

    // --- OutdoorScore ---

    [Fact(Timeout = 5000)]
    public void OutdoorScore_pleasant_spring() =>
        Assert.InRange(IndexScorer.OutdoorScore(22, 50, 5, 3, 20, Prefs), 80, 100);

    [Fact(Timeout = 5000)]
    public void OutdoorScore_stormy_winter() =>
        Assert.InRange(IndexScorer.OutdoorScore(2, 90, 90, 12, 100, Prefs), 0, 10);

    // --- RunningComfort ---

    [Fact(Timeout = 5000)]
    public void RunningComfort_ideal() =>
        Assert.InRange(IndexScorer.RunningComfort(12, 45, 2, 0, Prefs), 85, 100);

    [Fact(Timeout = 5000)]
    public void RunningComfort_hot_humid() =>
        Assert.InRange(IndexScorer.RunningComfort(35, 80, 0.5, 10, Prefs), 0, 55);

    // --- CyclingComfort ---

    [Fact(Timeout = 5000)]
    public void CyclingComfort_calm_warm() =>
        Assert.InRange(IndexScorer.CyclingComfort(18, 50, 1.5, 0, Prefs), 80, 100);

    [Fact(Timeout = 5000)]
    public void CyclingComfort_very_windy() =>
        Assert.InRange(IndexScorer.CyclingComfort(18, 50, 12, 0, Prefs), 0, 65);

    // --- BbqWeather ---

    [Fact(Timeout = 5000)]
    public void BbqWeather_perfect() =>
        Assert.InRange(IndexScorer.BbqWeather(26, 40, 2, 0, Prefs), 90, 100);

    [Fact(Timeout = 5000)]
    public void BbqWeather_rain_kills_it() =>
        Assert.InRange(IndexScorer.BbqWeather(26, 40, 2, 80, Prefs), 0, 70);

    // --- IrrigationNeed ---

    [Fact(Timeout = 5000)]
    public void IrrigationNeed_hot_dry() =>
        Assert.InRange(IndexScorer.IrrigationNeed(0, 32, 30, 6, Prefs), 85, 100);

    [Fact(Timeout = 5000)]
    public void IrrigationNeed_rainy() =>
        Assert.InRange(IndexScorer.IrrigationNeed(90, 15, 80, 1, Prefs), 0, 15);

    // --- SolarYield ---

    [Fact(Timeout = 5000)]
    public void SolarYield_clear_cool() =>
        Assert.InRange(IndexScorer.SolarYield(800, 10, 18, Prefs), 85, 100);

    [Fact(Timeout = 5000)]
    public void SolarYield_overcast_hot() =>
        Assert.InRange(IndexScorer.SolarYield(150, 90, 38, Prefs), 0, 20);

    // --- NightVentilation ---

    [Fact(Timeout = 5000)]
    public void NightVentilation_cool_evening() =>
        Assert.InRange(IndexScorer.NightVentilation(17, 45, 3, 0, Prefs), 75, 100);

    [Fact(Timeout = 5000)]
    public void NightVentilation_hot_humid() =>
        Assert.InRange(IndexScorer.NightVentilation(30, 80, 1, 0, Prefs), 0, 35);

    // --- FrostProtection ---

    [Fact(Timeout = 5000)]
    public void FrostProtection_frost_in_8_hours()
    {
        var points = Enumerable.Range(0, 48).Select(h =>
            new ForecastPoint(T0.AddHours(h), new Dictionary<ParameterDef, double?>
            {
                [Temperature] = h == 8 ? -1.0 : 10.0,
            })).ToList();
        var series = new ForecastSeries(points);

        var result = IndexScorer.FrostProtection([series], Temperature, T0);
        Assert.NotNull(result);
        Assert.Equal(8, result.HoursUntilFrost);
    }

    [Fact(Timeout = 5000)]
    public void FrostProtection_no_frost()
    {
        var points = Enumerable.Range(0, 48).Select(h =>
            new ForecastPoint(T0.AddHours(h), new Dictionary<ParameterDef, double?>
            {
                [Temperature] = 15.0,
            })).ToList();
        var series = new ForecastSeries(points);

        Assert.Null(IndexScorer.FrostProtection([series], Temperature, T0));
    }

    // --- VpdCategory ---

    [Fact(Timeout = 5000)]
    public void VpdCategory_high()
    {
        var result = IndexScorer.VpdCategory(25, 60);
        Assert.NotNull(result);
        Assert.Equal("high", result.Category);
        Assert.InRange(result.Vpd, 1.2, 1.4);
    }

    [Fact(Timeout = 5000)]
    public void VpdCategory_low()
    {
        var result = IndexScorer.VpdCategory(20, 90);
        Assert.NotNull(result);
        Assert.Equal("low", result.Category);
    }

    [Fact(Timeout = 5000)]
    public void VpdCategory_null() =>
        Assert.Null(IndexScorer.VpdCategory(null, 60));

    // --- Outdoor: Schwül fix (hot + humid + windstill) ---

    [Fact(Timeout = 5000)]
    public void OutdoorScore_schwuel_day_scores_low()
    {
        var score = IndexScorer.OutdoorScore(33, 85, 10, 0.5, 30, Prefs);
        Assert.InRange(score, 0, 40);
    }

    [Fact(Timeout = 5000)]
    public void OutdoorScore_schwuel_day_with_high_sensitivity_scores_lower()
    {
        var highSens = Prefs with { HeatSensitivity = 1.5, HumiditySensitivity = 1.3 };
        var score = IndexScorer.OutdoorScore(33, 85, 10, 0.5, 30, highSens);
        Assert.InRange(score, 0, 32);
    }

    [Fact(Timeout = 5000)]
    public void OutdoorScore_shifted_ideal_temp()
    {
        var shifted = Prefs with { IdealTemp = 26.0 };
        var scoreAt26 = IndexScorer.OutdoorScore(26, 45, 5, 3, 20, shifted);
        var scoreAt22 = IndexScorer.OutdoorScore(26, 45, 5, 3, 20, Prefs);
        Assert.True(scoreAt26 > scoreAt22, "Shifted ideal should yield higher score at 26°C");
        Assert.InRange(scoreAt26, 85, 100);
    }

    // --- BreezeScore ---

    [Fact(Timeout = 5000)]
    public void BreezeScore_ideal_range_scores_100()
    {
        Assert.Equal(100, IndexScorer.BreezeScore(3, 1.0));
    }

    [Fact(Timeout = 5000)]
    public void BreezeScore_windstill_penalized()
    {
        var score = IndexScorer.BreezeScore(0, 1.0);
        Assert.InRange(score, 20, 40);
    }

    [Fact(Timeout = 5000)]
    public void BreezeScore_strong_wind_penalized()
    {
        var score = IndexScorer.BreezeScore(10, 1.0);
        Assert.InRange(score, 0, 40);
    }

    // --- Sensitivity multipliers ---

    [Fact(Timeout = 5000)]
    public void Higher_heat_sensitivity_lowers_outdoor_score_in_heat()
    {
        var normal = IndexScorer.OutdoorScore(32, 50, 5, 3, 30, Prefs);
        var sensitive = IndexScorer.OutdoorScore(32, 50, 5, 3, 30, Prefs with { HeatSensitivity = 2.0 });
        Assert.True(sensitive < normal, "Higher heat sensitivity should produce lower score in heat");
    }

    [Fact(Timeout = 5000)]
    public void Higher_humidity_sensitivity_lowers_laundry_score()
    {
        var normal = IndexScorer.LaundryDrying(20, 55, 3, 10, 60, Prefs);
        var sensitive = IndexScorer.LaundryDrying(20, 55, 3, 10, 60, Prefs with { HumiditySensitivity = 2.0 });
        Assert.True(sensitive < normal, "Higher humidity sensitivity should produce lower score");
    }

    [Fact(Timeout = 5000)]
    public void Higher_rain_sensitivity_lowers_bbq_score()
    {
        var normal = IndexScorer.BbqWeather(25, 40, 2, 15, Prefs);
        var sensitive = IndexScorer.BbqWeather(25, 40, 2, 15, Prefs with { RainSensitivity = 2.0 });
        Assert.True(sensitive < normal, "Higher rain sensitivity should produce lower BBQ score");
    }

    // --- Score-specific ideal points ---

    [Fact(Timeout = 5000)]
    public void Running_custom_temp_range()
    {
        var cold = Prefs with { IdealTempLow = 0, IdealTempHigh = 15 };
        var scoreCustom = IndexScorer.RunningComfort(3, 50, 2, 0, cold);
        var scoreDefault = IndexScorer.RunningComfort(3, 50, 2, 0, Prefs);
        Assert.True(scoreCustom > scoreDefault, "Wider cold range should accept 3°C better");
    }

    [Fact(Timeout = 5000)]
    public void Bbq_custom_min_temp()
    {
        var strict = Prefs with { MinTemp = 15.0 };
        var scoreStrict = IndexScorer.BbqWeather(12, 40, 2, 0, strict);
        var scoreDefault = IndexScorer.BbqWeather(12, 40, 2, 0, Prefs);
        Assert.True(scoreStrict < scoreDefault, "Higher min temp should penalize 12°C more");
    }

    [Fact(Timeout = 5000)]
    public void NightVentilation_custom_indoor_temp()
    {
        var warm = Prefs with { IndoorTemp = 25.0 };
        var scoreCoolOutdoor = IndexScorer.NightVentilation(15, 45, 3, 0, warm);
        var scoreDefault = IndexScorer.NightVentilation(15, 45, 3, 0, Prefs);
        Assert.True(scoreCoolOutdoor > scoreDefault, "Higher indoor temp with cool outdoor should increase night ventilation score");
    }
}
