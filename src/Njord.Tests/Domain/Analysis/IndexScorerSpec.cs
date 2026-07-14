using Njord.Domain.Weather;
using Njord.Domain.Analysis;

namespace Njord.Tests.Domain.Analysis;

public sealed class IndexScorerSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;

    // --- LaundryDrying ---

    [Fact(Timeout = 5000)]
    public void LaundryDrying_perfect_day() =>
        Assert.InRange(IndexScorer.LaundryDrying(28, 35, 5, 0, 100), 90, 100);

    [Fact(Timeout = 5000)]
    public void LaundryDrying_cold_rainy_day() =>
        Assert.InRange(IndexScorer.LaundryDrying(5, 90, 1, 80, 0), 0, 15);

    // --- OutdoorScore ---

    [Fact(Timeout = 5000)]
    public void OutdoorScore_pleasant_spring() =>
        Assert.InRange(IndexScorer.OutdoorScore(22, 5, 2, 20), 85, 100);

    [Fact(Timeout = 5000)]
    public void OutdoorScore_stormy_winter() =>
        Assert.InRange(IndexScorer.OutdoorScore(2, 90, 12, 100), 0, 10);

    // --- RunningComfort ---

    [Fact(Timeout = 5000)]
    public void RunningComfort_ideal() =>
        Assert.InRange(IndexScorer.RunningComfort(12, 45, 2, 0), 85, 100);

    [Fact(Timeout = 5000)]
    public void RunningComfort_hot_humid() =>
        Assert.InRange(IndexScorer.RunningComfort(35, 80, 0.5, 10), 0, 55);

    // --- CyclingComfort ---

    [Fact(Timeout = 5000)]
    public void CyclingComfort_calm_warm() =>
        Assert.InRange(IndexScorer.CyclingComfort(18, 50, 1.5, 0), 85, 100);

    [Fact(Timeout = 5000)]
    public void CyclingComfort_very_windy() =>
        Assert.InRange(IndexScorer.CyclingComfort(18, 50, 12, 0), 0, 65);

    // --- BbqWeather ---

    [Fact(Timeout = 5000)]
    public void BbqWeather_perfect() =>
        Assert.InRange(IndexScorer.BbqWeather(26, 40, 2, 0), 90, 100);

    [Fact(Timeout = 5000)]
    public void BbqWeather_rain_kills_it() =>
        Assert.InRange(IndexScorer.BbqWeather(26, 40, 2, 80), 0, 70);

    // --- IrrigationNeed ---

    [Fact(Timeout = 5000)]
    public void IrrigationNeed_hot_dry() =>
        Assert.InRange(IndexScorer.IrrigationNeed(0, 32, 30, 6), 85, 100);

    [Fact(Timeout = 5000)]
    public void IrrigationNeed_rainy() =>
        Assert.InRange(IndexScorer.IrrigationNeed(90, 15, 80, 1), 0, 15);

    // --- DegreeDays ---

    [Fact(Timeout = 5000)]
    public void HeatingDegreeDays_cold() =>
        Assert.Equal(13.0, IndexScorer.HeatingDegreeDays(5, 18));

    [Fact(Timeout = 5000)]
    public void CoolingDegreeDays_hot() =>
        Assert.Equal(6.0, IndexScorer.CoolingDegreeDays(30, 24));

    [Fact(Timeout = 5000)]
    public void DegreeDays_mild()
    {
        Assert.Equal(0.0, IndexScorer.HeatingDegreeDays(20, 18));
        Assert.Equal(0.0, IndexScorer.CoolingDegreeDays(20, 24));
    }

    // --- SolarYield ---

    [Fact(Timeout = 5000)]
    public void SolarYield_clear_cool() =>
        Assert.InRange(IndexScorer.SolarYield(800, 10, 18), 85, 100);

    [Fact(Timeout = 5000)]
    public void SolarYield_overcast_hot() =>
        Assert.InRange(IndexScorer.SolarYield(150, 90, 38), 0, 20);

    // --- Ventilation ---

    [Fact(Timeout = 5000)]
    public void Ventilation_cool_evening() =>
        Assert.InRange(IndexScorer.Ventilation(17, 22, 45, 3, 0), 75, 100);

    [Fact(Timeout = 5000)]
    public void Ventilation_hot_humid() =>
        Assert.InRange(IndexScorer.Ventilation(30, 22, 80, 1, 0), 0, 35);

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
        Assert.Equal(8, result.Value.HoursUntilFrost);
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
        Assert.Equal("high", result.Value.Category);
        Assert.InRange(result.Value.Vpd, 1.2, 1.4);
    }

    [Fact(Timeout = 5000)]
    public void VpdCategory_low()
    {
        var result = IndexScorer.VpdCategory(20, 90);
        Assert.NotNull(result);
        Assert.Equal("low", result.Value.Category);
    }

    [Fact(Timeout = 5000)]
    public void VpdCategory_null() =>
        Assert.Null(IndexScorer.VpdCategory(null, 60));
}
