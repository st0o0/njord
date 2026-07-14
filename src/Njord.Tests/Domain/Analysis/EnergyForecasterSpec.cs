using Njord.Domain.Weather;
using Njord.Enrichment;
using Njord.Domain.Analysis;

namespace Njord.Tests.Domain.Analysis;

public sealed class EnergyForecasterSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
    private static readonly ParameterDef Humidity = ParameterRegistry.GetByApiName("relative_humidity_2m")!;
    private static readonly ParameterDef WindSpeed = ParameterRegistry.GetByApiName("wind_speed_10m")!;
    private static readonly ParameterDef PrecipProb = ParameterRegistry.GetByApiName("precipitation_probability")!;

    // --- HeatingDemand ---

    [Fact(Timeout = 5000)]
    public void HeatingDemand_cold_windy_overcast() =>
        Assert.InRange(EnergyForecaster.HeatingDemand(0, 8, 100), 80, 100);

    [Fact(Timeout = 5000)]
    public void HeatingDemand_mild_calm_clear() =>
        Assert.InRange(EnergyForecaster.HeatingDemand(18, 1, 10), 0, 15);

    // --- CopEstimate ---

    [Fact(Timeout = 5000)]
    public void CopEstimate_mild()
    {
        var cop = EnergyForecaster.CopEstimate(10, 35, 0.45);
        Assert.NotNull(cop);
        Assert.InRange(cop.Value, 5.4, 5.7);
    }

    [Fact(Timeout = 5000)]
    public void CopEstimate_cold()
    {
        var cop = EnergyForecaster.CopEstimate(-10, 35, 0.45);
        Assert.NotNull(cop);
        Assert.InRange(cop.Value, 3.0, 3.2);
    }

    [Fact(Timeout = 5000)]
    public void CopEstimate_above_flow_returns_null() =>
        Assert.Null(EnergyForecaster.CopEstimate(40, 35));

    [Fact(Timeout = 5000)]
    public void CopEstimate_null_returns_null() =>
        Assert.Null(EnergyForecaster.CopEstimate(null));

    // --- CopOptimalHours ---

    [Fact(Timeout = 5000)]
    public void CopOptimalHours_warmest_ranked_first()
    {
        var points = Enumerable.Range(0, 24).Select(h =>
            new ForecastPoint(T0.AddHours(h), new Dictionary<ParameterDef, double?>
            {
                [Temperature] = -5 + h,
            })).ToList();
        var series = new ForecastSeries(points);

        var result = EnergyForecaster.CopOptimalHours(series, Temperature, 35, 0.45, 3, T0);
        Assert.Equal(3, result.Count);
        Assert.True(result[0].Cop >= result[1].Cop);
        Assert.True(result[1].Cop >= result[2].Cop);
    }

    // --- ShadingScore ---

    [Fact(Timeout = 5000)]
    public void ShadingScore_peak_summer() =>
        Assert.InRange(EnergyForecaster.ShadingScore(800, 1.0, 32), 60, 100);

    [Fact(Timeout = 5000)]
    public void ShadingScore_overcast_cool() =>
        Assert.InRange(EnergyForecaster.ShadingScore(100, 1.0, 15), 0, 20);

    [Fact(Timeout = 5000)]
    public void ShadingScore_night() =>
        Assert.InRange(EnergyForecaster.ShadingScore(0, 0.0, 20), 0, 15);

    // --- BatteryStrategy ---

    [Fact(Timeout = 5000)]
    public void BatteryStrategy_charge() =>
        Assert.Equal("charge", EnergyForecaster.BatteryStrategy(85, 1.0));

    [Fact(Timeout = 5000)]
    public void BatteryStrategy_discharge_night() =>
        Assert.Equal("discharge", EnergyForecaster.BatteryStrategy(0, 0.0));

    [Fact(Timeout = 5000)]
    public void BatteryStrategy_hold() =>
        Assert.Equal("hold", EnergyForecaster.BatteryStrategy(40, 1.0));

    [Fact(Timeout = 5000)]
    public void BatteryStrategy_discharge_low_solar() =>
        Assert.Equal("discharge", EnergyForecaster.BatteryStrategy(15, 1.0));

    // --- NightCoolingPotential ---

    [Fact(Timeout = 5000)]
    public void NightCooling_cool_dry_night()
    {
        var points = Enumerable.Range(0, 48).Select(h =>
        {
            var hour = (T0.Hour + h) % 24;
            return new ForecastPoint(T0.AddHours(h), new Dictionary<ParameterDef, double?>
            {
                [Temperature] = hour is >= 22 or < 6 ? 16.0 : 28.0,
                [Humidity] = 40.0,
                [WindSpeed] = 3.0,
                [PrecipProb] = 0.0,
            });
        }).ToList();
        var series = new ForecastSeries(points);

        var result = EnergyForecaster.NightCoolingPotential(
            series, Temperature, Humidity, WindSpeed, PrecipProb, 22, T0);
        Assert.InRange(result, 70, 100);
    }

    [Fact(Timeout = 5000)]
    public void NightCooling_warm_humid_night()
    {
        var points = Enumerable.Range(0, 48).Select(h =>
        {
            var hour = (T0.Hour + h) % 24;
            return new ForecastPoint(T0.AddHours(h), new Dictionary<ParameterDef, double?>
            {
                [Temperature] = hour is >= 22 or < 6 ? 25.0 : 32.0,
                [Humidity] = 80.0,
                [WindSpeed] = 0.5,
                [PrecipProb] = 30.0,
            });
        }).ToList();
        var series = new ForecastSeries(points);

        var result = EnergyForecaster.NightCoolingPotential(
            series, Temperature, Humidity, WindSpeed, PrecipProb, 22, T0);
        Assert.InRange(result, 0, 25);
    }
}
