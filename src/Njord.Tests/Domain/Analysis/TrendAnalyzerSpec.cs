using Njord.Domain.Weather;
using Njord.Domain.Analysis;

namespace Njord.Tests.Domain.Analysis;

public sealed class TrendAnalyzerSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
    private static readonly ParameterDef Precipitation = ParameterRegistry.GetByApiName("precipitation")!;

    // --- TrendDirection ---

    [Fact(Timeout = 5000)]
    public void TrendDirection_rising()
    {
        var result = TrendAnalyzer.TrendDirection(18.0, 22.0, 0.5);
        Assert.NotNull(result);
        Assert.Equal("rising", result.Value.Direction);
        Assert.Equal(4.0, result.Value.Delta);
    }

    [Fact(Timeout = 5000)]
    public void TrendDirection_falling()
    {
        var result = TrendAnalyzer.TrendDirection(22.0, 18.0, 0.5);
        Assert.NotNull(result);
        Assert.Equal("falling", result.Value.Direction);
        Assert.Equal(-4.0, result.Value.Delta);
    }

    [Fact(Timeout = 5000)]
    public void TrendDirection_stable_within_deadband()
    {
        var result = TrendAnalyzer.TrendDirection(20.0, 20.3, 0.5);
        Assert.NotNull(result);
        Assert.Equal("stable", result.Value.Direction);
        Assert.Equal(0.3, result.Value.Delta);
    }

    [Fact(Timeout = 5000)]
    public void TrendDirection_null_previous() =>
        Assert.Null(TrendAnalyzer.TrendDirection(null, 20.0, 0.5));

    [Fact(Timeout = 5000)]
    public void TrendDirection_null_current() =>
        Assert.Null(TrendAnalyzer.TrendDirection(20.0, null, 0.5));

    // --- WeatherChange ---

    [Fact(Timeout = 5000)]
    public void WeatherChange_clear_to_rain()
    {
        var result = TrendAnalyzer.WeatherChange(1, 63);
        Assert.NotNull(result);
        Assert.Equal("clear", result.FromCategory);
        Assert.Equal("rain", result.ToCategory);
        Assert.Equal("clear → rain", result.Description);
    }

    [Fact(Timeout = 5000)]
    public void WeatherChange_same_category_returns_null() =>
        Assert.Null(TrendAnalyzer.WeatherChange(61, 65));

    [Fact(Timeout = 5000)]
    public void WeatherChange_null_codes() =>
        Assert.Null(TrendAnalyzer.WeatherChange(null, 63));

    // --- PrecipitationTiming ---

    [Fact(Timeout = 5000)]
    public void PrecipitationTiming_rain_window()
    {
        var points = Enumerable.Range(0, 24).Select(h =>
            new ForecastPoint(T0.AddHours(h), new Dictionary<ParameterDef, double?>
            {
                [Precipitation] = h >= 3 && h <= 8 ? 2.5 : 0.0,
            })).ToList();
        var series = new ForecastSeries(points);

        var (starts, ends) = TrendAnalyzer.PrecipitationTiming(series, Precipitation, T0);
        Assert.Equal(3, starts);
        Assert.Equal(8, ends);
    }

    [Fact(Timeout = 5000)]
    public void PrecipitationTiming_no_precipitation()
    {
        var points = Enumerable.Range(0, 24).Select(h =>
            new ForecastPoint(T0.AddHours(h), new Dictionary<ParameterDef, double?>
            {
                [Precipitation] = 0.0,
            })).ToList();
        var series = new ForecastSeries(points);

        var (starts, ends) = TrendAnalyzer.PrecipitationTiming(series, Precipitation, T0);
        Assert.Null(starts);
        Assert.Null(ends);
    }

    [Fact(Timeout = 5000)]
    public void PrecipitationTiming_from_start()
    {
        var points = Enumerable.Range(0, 24).Select(h =>
            new ForecastPoint(T0.AddHours(h), new Dictionary<ParameterDef, double?>
            {
                [Precipitation] = h <= 12 ? 1.0 : 0.0,
            })).ToList();
        var series = new ForecastSeries(points);

        var (starts, ends) = TrendAnalyzer.PrecipitationTiming(series, Precipitation, T0);
        Assert.Equal(0, starts);
        Assert.Equal(12, ends);
    }

    // --- ExtremaTiming ---

    [Fact(Timeout = 5000)]
    public void ExtremaTiming_peak_and_low()
    {
        var points = Enumerable.Range(0, 24).Select(h =>
            new ForecastPoint(T0.AddHours(h), new Dictionary<ParameterDef, double?>
            {
                [Temperature] = h == 6 ? 28.0 : h == 18 ? 12.0 : 20.0,
            })).ToList();
        var series = new ForecastSeries(points);

        var (maxH, minH) = TrendAnalyzer.ExtremaTiming(series, Temperature, T0);
        Assert.Equal(6, maxH);
        Assert.Equal(18, minH);
    }

    [Fact(Timeout = 5000)]
    public void ExtremaTiming_insufficient_data()
    {
        var points = new[] { new ForecastPoint(T0, new Dictionary<ParameterDef, double?> { [Temperature] = 20.0 }) };
        var series = new ForecastSeries(points);

        var (maxH, minH) = TrendAnalyzer.ExtremaTiming(series, Temperature, T0);
        Assert.Null(maxH);
        Assert.Null(minH);
    }

    // --- ConsensusStability ---

    [Fact(Timeout = 5000)]
    public void ConsensusStability_converging()
    {
        var result = TrendAnalyzer.ConsensusStability(5.0, 3.0);
        Assert.NotNull(result);
        Assert.Equal("converging", result.Value.Label);
        Assert.Equal(0.6, result.Value.Ratio);
    }

    [Fact(Timeout = 5000)]
    public void ConsensusStability_diverging()
    {
        var result = TrendAnalyzer.ConsensusStability(3.0, 5.0);
        Assert.NotNull(result);
        Assert.Equal("diverging", result.Value.Label);
        Assert.InRange(result.Value.Ratio, 1.66, 1.68);
    }

    [Fact(Timeout = 5000)]
    public void ConsensusStability_stable()
    {
        var result = TrendAnalyzer.ConsensusStability(4.0, 4.2);
        Assert.NotNull(result);
        Assert.Equal("stable", result.Value.Label);
        Assert.Equal(1.05, result.Value.Ratio);
    }

    [Fact(Timeout = 5000)]
    public void ConsensusStability_null_iqr() =>
        Assert.Null(TrendAnalyzer.ConsensusStability(null, 3.0));

    [Fact(Timeout = 5000)]
    public void ConsensusStability_zero_previous() =>
        Assert.Null(TrendAnalyzer.ConsensusStability(0.0, 3.0));

    // --- PredictabilityDecay ---

    [Fact(Timeout = 5000)]
    public void PredictabilityDecay_gradual()
    {
        var spreads = new (int, double?)[]
            { (3, 1.0), (6, 1.5), (12, 2.5), (24, 4.0), (48, 7.0), (72, 10.0) };

        var result = TrendAnalyzer.PredictabilityDecay(spreads);
        Assert.NotNull(result);
        Assert.True(result.Value.DecayRate > 0);
        Assert.Equal(24, result.Value.ReliableHours);
    }

    [Fact(Timeout = 5000)]
    public void PredictabilityDecay_flat()
    {
        var spreads = new (int, double?)[]
            { (3, 2.0), (6, 2.1), (12, 2.0), (24, 2.2) };

        var result = TrendAnalyzer.PredictabilityDecay(spreads);
        Assert.NotNull(result);
        Assert.InRange(result.Value.DecayRate, -0.01, 0.02);
        Assert.Null(result.Value.ReliableHours);
    }

    [Fact(Timeout = 5000)]
    public void PredictabilityDecay_insufficient_data() =>
        Assert.Null(TrendAnalyzer.PredictabilityDecay([(3, 1.0)]));
}
