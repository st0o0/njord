using Njord.Domain.Weather;
using Njord.Domain.Analysis;

namespace Njord.Tests.Domain.Analysis;

public sealed class DerivedComputerSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
    private static readonly ParameterDef SunshineDuration = ParameterRegistry.GetByApiName("sunshine_duration")!;
    private static readonly ParameterDef IsDay = ParameterRegistry.GetByApiName("is_day")!;

    // --- Beaufort ---

    [Fact(Timeout = 5000)]
    public void Beaufort_calm_wind() =>
        Assert.Equal(0, DerivedComputer.Beaufort(0.2));

    [Fact(Timeout = 5000)]
    public void Beaufort_light_breeze() =>
        Assert.Equal(2, DerivedComputer.Beaufort(2.5));

    [Fact(Timeout = 5000)]
    public void Beaufort_strong_gale() =>
        Assert.Equal(9, DerivedComputer.Beaufort(22.0));

    [Fact(Timeout = 5000)]
    public void Beaufort_hurricane_force() =>
        Assert.Equal(12, DerivedComputer.Beaufort(35.0));

    [Fact(Timeout = 5000)]
    public void Beaufort_null_returns_null() =>
        Assert.Null(DerivedComputer.Beaufort(null));

    // --- WindChill ---

    [Fact(Timeout = 5000)]
    public void WindChill_cold_and_windy()
    {
        var result = DerivedComputer.WindChill(-5.0, 5.0);
        Assert.NotNull(result);
        Assert.InRange(result.Value, -12.0, -10.0);
    }

    [Fact(Timeout = 5000)]
    public void WindChill_mild_temperature_returns_null() =>
        Assert.Null(DerivedComputer.WindChill(15.0, 5.0));

    [Fact(Timeout = 5000)]
    public void WindChill_calm_wind_returns_null() =>
        Assert.Null(DerivedComputer.WindChill(-5.0, 1.0));

    [Fact(Timeout = 5000)]
    public void WindChill_null_inputs_returns_null()
    {
        Assert.Null(DerivedComputer.WindChill(null, 5.0));
        Assert.Null(DerivedComputer.WindChill(-5.0, null));
    }

    // --- DewPointComfort ---

    [Fact(Timeout = 5000)]
    public void DewPointComfort_dry() =>
        Assert.Equal("dry", DerivedComputer.DewPointComfort(5.0));

    [Fact(Timeout = 5000)]
    public void DewPointComfort_comfortable() =>
        Assert.Equal("comfortable", DerivedComputer.DewPointComfort(12.0));

    [Fact(Timeout = 5000)]
    public void DewPointComfort_sticky() =>
        Assert.Equal("sticky", DerivedComputer.DewPointComfort(17.0));

    [Fact(Timeout = 5000)]
    public void DewPointComfort_oppressive() =>
        Assert.Equal("oppressive", DerivedComputer.DewPointComfort(20.0));

    [Fact(Timeout = 5000)]
    public void DewPointComfort_dangerous() =>
        Assert.Equal("dangerous", DerivedComputer.DewPointComfort(23.0));

    [Fact(Timeout = 5000)]
    public void DewPointComfort_boundary_at_10() =>
        Assert.Equal("comfortable", DerivedComputer.DewPointComfort(10.0));

    [Fact(Timeout = 5000)]
    public void DewPointComfort_null_returns_null() =>
        Assert.Null(DerivedComputer.DewPointComfort(null));

    // --- DiurnalAmplitude ---

    [Fact(Timeout = 5000)]
    public void DiurnalAmplitude_normal_range()
    {
        var points = Enumerable.Range(0, 24).Select(h =>
            new ForecastPoint(T0.AddHours(h), new Dictionary<ParameterDef, double?>
            {
                [Temperature] = 8.0 + (h < 12 ? h : 24 - h),
            })).ToList();
        var series = new ForecastSeries(points);

        var result = DerivedComputer.DiurnalAmplitude(series, Temperature, T0);
        Assert.Equal(12.0, result);
    }

    [Fact(Timeout = 5000)]
    public void DiurnalAmplitude_insufficient_data()
    {
        var points = new[] { new ForecastPoint(T0, new Dictionary<ParameterDef, double?> { [Temperature] = 10.0 }) };
        var series = new ForecastSeries(points);

        Assert.Null(DerivedComputer.DiurnalAmplitude(series, Temperature, T0));
    }

    // --- SunshinePercent ---

    [Fact(Timeout = 5000)]
    public void SunshinePercent_full_sunshine()
    {
        var points = Enumerable.Range(0, 24).Select(h =>
            new ForecastPoint(T0.AddHours(h), new Dictionary<ParameterDef, double?>
            {
                [IsDay] = h >= 5 && h < 19 ? 1.0 : 0.0,
                [SunshineDuration] = h >= 5 && h < 19 ? 3600.0 : 0.0,
            })).ToList();
        var series = new ForecastSeries(points);

        var result = DerivedComputer.SunshinePercent(series, SunshineDuration, IsDay, T0);
        Assert.Equal(100.0, result);
    }

    [Fact(Timeout = 5000)]
    public void SunshinePercent_partial_sunshine()
    {
        var points = Enumerable.Range(0, 24).Select(h =>
            new ForecastPoint(T0.AddHours(h), new Dictionary<ParameterDef, double?>
            {
                [IsDay] = h >= 5 && h < 19 ? 1.0 : 0.0,
                [SunshineDuration] = h >= 5 && h < 12 ? 3600.0 : 0.0,
            })).ToList();
        var series = new ForecastSeries(points);

        var result = DerivedComputer.SunshinePercent(series, SunshineDuration, IsDay, T0);
        Assert.Equal(50.0, result);
    }

    [Fact(Timeout = 5000)]
    public void SunshinePercent_no_sunshine_data()
    {
        var points = Enumerable.Range(0, 24).Select(h =>
            new ForecastPoint(T0.AddHours(h), new Dictionary<ParameterDef, double?>
            {
                [IsDay] = 1.0,
            })).ToList();
        var series = new ForecastSeries(points);

        Assert.Null(DerivedComputer.SunshinePercent(series, SunshineDuration, IsDay, T0));
    }

    [Fact(Timeout = 5000)]
    public void SunshinePercent_no_daylight()
    {
        var points = Enumerable.Range(0, 24).Select(h =>
            new ForecastPoint(T0.AddHours(h), new Dictionary<ParameterDef, double?>
            {
                [IsDay] = 0.0,
                [SunshineDuration] = 0.0,
            })).ToList();
        var series = new ForecastSeries(points);

        Assert.Null(DerivedComputer.SunshinePercent(series, SunshineDuration, IsDay, T0));
    }

    // --- WmoDescription ---

    [Fact(Timeout = 5000)]
    public void WmoDescription_clear_sky() =>
        Assert.Equal("Clear sky", DerivedComputer.WmoDescription(0));

    [Fact(Timeout = 5000)]
    public void WmoDescription_mainly_clear() =>
        Assert.Equal("Mainly clear", DerivedComputer.WmoDescription(1));

    [Fact(Timeout = 5000)]
    public void WmoDescription_rain_slight() =>
        Assert.Equal("Rain: slight", DerivedComputer.WmoDescription(61));

    [Fact(Timeout = 5000)]
    public void WmoDescription_thunderstorm_heavy_hail() =>
        Assert.Equal("Thunderstorm with heavy hail", DerivedComputer.WmoDescription(99));

    [Fact(Timeout = 5000)]
    public void WmoDescription_unknown_code() =>
        Assert.Null(DerivedComputer.WmoDescription(150));

    [Fact(Timeout = 5000)]
    public void WmoDescription_null_returns_null() =>
        Assert.Null(DerivedComputer.WmoDescription(null));

    // --- InversionDetected ---

    [Fact(Timeout = 5000)]
    public void Inversion_conditions_met() =>
        Assert.True(DerivedComputer.InversionDetected(1020, 1015, 2.0, 1.0));

    [Fact(Timeout = 5000)]
    public void Inversion_dry_air_no_inversion() =>
        Assert.False(DerivedComputer.InversionDetected(1020, 1015, 10.0, 2.0));

    [Fact(Timeout = 5000)]
    public void Inversion_low_pressure_gap_no_inversion() =>
        Assert.False(DerivedComputer.InversionDetected(1016, 1015, 2.0, 1.0));

    [Fact(Timeout = 5000)]
    public void Inversion_null_inputs_returns_null()
    {
        Assert.Null(DerivedComputer.InversionDetected(null, 1015, 2.0, 1.0));
        Assert.Null(DerivedComputer.InversionDetected(1020, null, 2.0, 1.0));
        Assert.Null(DerivedComputer.InversionDetected(1020, 1015, null, 1.0));
        Assert.Null(DerivedComputer.InversionDetected(1020, 1015, 2.0, null));
    }
}
