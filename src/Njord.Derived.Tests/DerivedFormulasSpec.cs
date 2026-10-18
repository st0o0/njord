using Njord.Consensus;
using Njord.Messages.Derived;
using Njord.Weather;

namespace Njord.Derived.Tests;

public sealed class DerivedFormulasSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
    private static readonly ParameterDef SunshineDuration = ParameterRegistry.GetByApiName("sunshine_duration")!;
    private static readonly ParameterDef IsDay = ParameterRegistry.GetByApiName("is_day")!;

    // --- Beaufort ---

    [Fact]
    public void Beaufort_calm_wind() =>
        Assert.Equal(0, DerivedFormulas.Beaufort(0.2));

    [Fact]
    public void Beaufort_light_breeze() =>
        Assert.Equal(2, DerivedFormulas.Beaufort(2.5));

    [Fact]
    public void Beaufort_strong_gale() =>
        Assert.Equal(9, DerivedFormulas.Beaufort(22.0));

    [Fact]
    public void Beaufort_hurricane_force() =>
        Assert.Equal(12, DerivedFormulas.Beaufort(35.0));

    [Fact]
    public void Beaufort_null_returns_null() =>
        Assert.Null(DerivedFormulas.Beaufort(null));

    // --- WindChill ---

    [Fact]
    public void WindChill_cold_and_windy()
    {
        var result = DerivedFormulas.WindChill(-5.0, 5.0);
        Assert.NotNull(result);
        Assert.InRange(result.Value, -12.0, -10.0);
    }

    [Fact]
    public void WindChill_mild_temperature_returns_null() =>
        Assert.Null(DerivedFormulas.WindChill(15.0, 5.0));

    [Fact]
    public void WindChill_calm_wind_returns_null() =>
        Assert.Null(DerivedFormulas.WindChill(-5.0, 1.0));

    [Fact]
    public void WindChill_null_inputs_returns_null()
    {
        Assert.Null(DerivedFormulas.WindChill(null, 5.0));
        Assert.Null(DerivedFormulas.WindChill(-5.0, null));
    }

    // --- DewPointComfort ---

    [Fact]
    public void DewPointComfort_dry() =>
        Assert.Equal(DewPointComfort.Dry, DerivedFormulas.Comfort(5.0));

    [Fact]
    public void DewPointComfort_comfortable() =>
        Assert.Equal(DewPointComfort.Comfortable, DerivedFormulas.Comfort(12.0));

    [Fact]
    public void DewPointComfort_sticky() =>
        Assert.Equal(DewPointComfort.Sticky, DerivedFormulas.Comfort(17.0));

    [Fact]
    public void DewPointComfort_oppressive() =>
        Assert.Equal(DewPointComfort.Oppressive, DerivedFormulas.Comfort(20.0));

    [Fact]
    public void DewPointComfort_dangerous() =>
        Assert.Equal(DewPointComfort.Dangerous, DerivedFormulas.Comfort(23.0));

    [Fact]
    public void DewPointComfort_boundary_at_10() =>
        Assert.Equal(DewPointComfort.Comfortable, DerivedFormulas.Comfort(10.0));

    [Fact]
    public void DewPointComfort_null_returns_null() =>
        Assert.Null(DerivedFormulas.Comfort(null));

    // --- DiurnalAmplitude ---

    [Fact]
    public void DiurnalAmplitude_normal_range()
    {
        var points = Enumerable.Range(0, 24).Select(h =>
            new ForecastPoint(T0.AddHours(h), new Dictionary<ParameterDef, double?>
            {
                [Temperature] = 8.0 + (h < 12 ? h : 24 - h),
            })).ToList();
        var series = new ForecastSeries(points);

        var result = DerivedFormulas.DiurnalAmplitude(series, Temperature, T0);
        Assert.Equal(12.0, result);
    }

    [Fact]
    public void DiurnalAmplitude_insufficient_data()
    {
        var points = new[] { new ForecastPoint(T0, new Dictionary<ParameterDef, double?> { [Temperature] = 10.0 }) };
        var series = new ForecastSeries(points);

        Assert.Null(DerivedFormulas.DiurnalAmplitude(series, Temperature, T0));
    }

    // --- SunshinePercent ---

    [Fact]
    public void SunshinePercent_full_sunshine()
    {
        var points = Enumerable.Range(0, 24).Select(h =>
            new ForecastPoint(T0.AddHours(h), new Dictionary<ParameterDef, double?>
            {
                [IsDay] = h is >= 5 and < 19 ? 1.0 : 0.0,
                [SunshineDuration] = h is >= 5 and < 19 ? 3600.0 : 0.0,
            })).ToList();
        var series = new ForecastSeries(points);

        var result = DerivedFormulas.SunshinePercent(series, SunshineDuration, IsDay, T0);
        Assert.Equal(100.0, result);
    }

    [Fact]
    public void SunshinePercent_partial_sunshine()
    {
        var points = Enumerable.Range(0, 24).Select(h =>
            new ForecastPoint(T0.AddHours(h), new Dictionary<ParameterDef, double?>
            {
                [IsDay] = h is >= 5 and < 19 ? 1.0 : 0.0,
                [SunshineDuration] = h is >= 5 and < 12 ? 3600.0 : 0.0,
            })).ToList();
        var series = new ForecastSeries(points);

        var result = DerivedFormulas.SunshinePercent(series, SunshineDuration, IsDay, T0);
        Assert.Equal(50.0, result);
    }

    [Fact]
    public void SunshinePercent_no_sunshine_data()
    {
        var points = Enumerable.Range(0, 24).Select(h =>
            new ForecastPoint(T0.AddHours(h), new Dictionary<ParameterDef, double?>
            {
                [IsDay] = 1.0,
            })).ToList();
        var series = new ForecastSeries(points);

        Assert.Null(DerivedFormulas.SunshinePercent(series, SunshineDuration, IsDay, T0));
    }

    [Fact]
    public void SunshinePercent_no_daylight()
    {
        var points = Enumerable.Range(0, 24).Select(h =>
            new ForecastPoint(T0.AddHours(h), new Dictionary<ParameterDef, double?>
            {
                [IsDay] = 0.0,
                [SunshineDuration] = 0.0,
            })).ToList();
        var series = new ForecastSeries(points);

        Assert.Null(DerivedFormulas.SunshinePercent(series, SunshineDuration, IsDay, T0));
    }

    // --- Condition ---

    [Fact]
    public void WmoDescription_clear_sky() =>
        Assert.Equal(WeatherCondition.ClearSky, DerivedFormulas.Condition(0));

    [Fact]
    public void WmoDescription_mainly_clear() =>
        Assert.Equal(WeatherCondition.MainlyClear, DerivedFormulas.Condition(1));

    [Fact]
    public void WmoDescription_rain_slight() =>
        Assert.Equal(WeatherCondition.RainSlight, DerivedFormulas.Condition(61));

    [Fact]
    public void WmoDescription_thunderstorm_heavy_hail() =>
        Assert.Equal(WeatherCondition.ThunderstormHeavyHail, DerivedFormulas.Condition(99));

    [Fact]
    public void WmoDescription_unknown_code() =>
        Assert.Null(DerivedFormulas.Condition(150));

    [Fact]
    public void WmoDescription_null_returns_null() =>
        Assert.Null(DerivedFormulas.Condition(null));

    // --- InversionDetected ---

    [Fact]
    public void Inversion_conditions_met() =>
        Assert.True(DerivedFormulas.InversionDetected(1020, 1015, 2.0, 1.0));

    [Fact]
    public void Inversion_dry_air_no_inversion() =>
        Assert.False(DerivedFormulas.InversionDetected(1020, 1015, 10.0, 2.0));

    [Fact]
    public void Inversion_low_pressure_gap_no_inversion() =>
        Assert.False(DerivedFormulas.InversionDetected(1016, 1015, 2.0, 1.0));

    [Fact]
    public void Inversion_null_inputs_returns_null()
    {
        Assert.Null(DerivedFormulas.InversionDetected(null, 1015, 2.0, 1.0));
        Assert.Null(DerivedFormulas.InversionDetected(1020, null, 2.0, 1.0));
        Assert.Null(DerivedFormulas.InversionDetected(1020, 1015, null, 1.0));
        Assert.Null(DerivedFormulas.InversionDetected(1020, 1015, 2.0, null));
    }
}
