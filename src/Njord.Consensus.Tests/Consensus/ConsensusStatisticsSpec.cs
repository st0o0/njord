using Njord.Consensus;
using Njord.Model;
using Njord.Forecast;
using WeatherForecast = Njord.Forecast.Forecast;

namespace Njord.Consensus.Tests.Consensus;

public sealed class ConsensusStatisticsSpec
{
    private static readonly WeatherModel IconD2 = new("icon_d2");
    private static readonly WeatherModel Ecmwf = new("ecmwf_ifs025");
    private static readonly WeatherModel Gfs = new("gfs_seamless");

    // --- ComputeMedian ---

    [Fact]
    public void Median_of_odd_count_returns_middle()
    {
        double?[] values = [19.0, 20.0, 21.0, 22.0, 23.0];
        Assert.Equal(21.0, ConsensusStatistics.ComputeMedian(values));
    }

    [Fact]
    public void Median_of_even_count_returns_average_of_two_middle()
    {
        double?[] values = [20.0, 21.0, 22.0, 23.0];
        Assert.Equal(21.5, ConsensusStatistics.ComputeMedian(values));
    }

    [Fact]
    public void Median_skips_nulls()
    {
        double?[] values = [20.0, null, 22.0, null, 21.0, 23.0, 19.0];
        Assert.Equal(21.0, ConsensusStatistics.ComputeMedian(values));
    }

    [Fact]
    public void Median_all_null_returns_null()
    {
        double?[] values = [null, null, null];
        Assert.Null(ConsensusStatistics.ComputeMedian(values));
    }

    [Fact]
    public void Median_single_value()
    {
        double?[] values = [42.0];
        Assert.Equal(42.0, ConsensusStatistics.ComputeMedian(values));
    }

    // --- ComputeTrimmedMean ---

    [Fact]
    public void Trimmed_mean_10_percent_on_8_values()
    {
        double?[] values = [10, 12, 14, 16, 18, 20, 22, 24];
        // floor(8 * 0.1) = 0, so no trimming → mean of all 8
        var result = ConsensusStatistics.ComputeTrimmedMean(values, 0.1);
        Assert.Equal(17.0, result);
    }

    [Fact]
    public void Trimmed_mean_20_percent_on_10_values()
    {
        double?[] values = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        // floor(10 * 0.2) = 2, trim 2 from each end → [3,4,5,6,7,8], mean = 5.5
        var result = ConsensusStatistics.ComputeTrimmedMean(values, 0.2);
        Assert.Equal(5.5, result);
    }

    [Fact]
    public void Trimmed_mean_fewer_than_3_falls_back_to_simple_mean()
    {
        double?[] values = [20.0, 22.0];
        Assert.Equal(21.0, ConsensusStatistics.ComputeTrimmedMean(values, 0.2));
    }

    // --- ComputeSpread ---

    [Fact]
    public void Spread_normal()
    {
        double?[] values = [18.0, 22.0, 20.0, null, 25.0];
        Assert.Equal(7.0, ConsensusStatistics.ComputeSpread(values));
    }

    [Fact]
    public void Spread_single_value_returns_null()
    {
        double?[] values = [20.0];
        Assert.Null(ConsensusStatistics.ComputeSpread(values));
    }

    // --- ComputeIqr ---

    [Fact]
    public void Iqr_8_values()
    {
        double?[] values = [18, 19, 20, 21, 22, 23, 24, 25];
        var result = ConsensusStatistics.ComputeIqr(values);
        Assert.NotNull(result);
        Assert.Equal(3.5, result.Value, 1);
    }

    [Fact]
    public void Iqr_fewer_than_4_returns_null()
    {
        double?[] values = [18.0, 20.0, 22.0];
        Assert.Null(ConsensusStatistics.ComputeIqr(values));
    }

    // --- ComputeAgreement ---

    [Fact]
    public void Agreement_all_within_tolerance()
    {
        double?[] values = [20.0, 20.5, 19.5, 20.2];
        Assert.Equal(1.0, ConsensusStatistics.ComputeAgreement(values, 20.1, 1.0));
    }

    [Fact]
    public void Agreement_partial()
    {
        double?[] values = [20.0, 25.0, 19.0, 30.0];
        Assert.Equal(0.5, ConsensusStatistics.ComputeAgreement(values, 22.5, 3.0));
    }

    [Fact]
    public void Agreement_empty_returns_null()
    {
        double?[] values = [null, null];
        Assert.Null(ConsensusStatistics.ComputeAgreement(values, 20.0, 1.0));
    }

    // --- IdentifyOutlier ---

    [Fact]
    public void Outlier_clear_deviation()
    {
        var models = new List<(WeatherModel, double?)>
        {
            (IconD2, 20.0), (Ecmwf, 21.0), (Gfs, 35.0),
        };
        var result = ConsensusStatistics.IdentifyOutlier(models, 21.0);
        Assert.NotNull(result);
        Assert.Equal(Gfs, result.Value.Model);
        Assert.Equal(14.0, result.Value.Deviation);
    }

    [Fact]
    public void Outlier_all_equal()
    {
        var models = new List<(WeatherModel, double?)>
        {
            (IconD2, 20.0), (Ecmwf, 20.0),
        };
        var result = ConsensusStatistics.IdentifyOutlier(models, 20.0);
        Assert.NotNull(result);
        Assert.Equal(0.0, result.Value.Deviation);
    }

    // --- ComputeConfidenceInterval ---

    [Fact]
    public void Confidence_interval_p10_p90_on_8_values()
    {
        double?[] values = [18, 19, 20, 21, 22, 23, 24, 25];
        var result = ConsensusStatistics.ComputeConfidenceInterval(values, 10, 90);
        Assert.NotNull(result);
        Assert.True(result.Value.Lower < result.Value.Upper);
    }

    [Fact]
    public void Confidence_interval_single_value_returns_null()
    {
        double?[] values = [20.0];
        Assert.Null(ConsensusStatistics.ComputeConfidenceInterval(values, 10, 90));
    }

    // --- BuildAvailabilityMatrix ---

    [Fact]
    public void Availability_model_with_data_at_horizon()
    {
        var t0 = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        var temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
        var point = new ForecastPoint(t0.AddHours(3), new Dictionary<ParameterDef, ForecastValue> { [temperature] = 22.5 });
        var forecast = new WeatherForecast(IconD2, "lucerne", new CycleId(t0),
            new ForecastSeries([point]), DailyForecastSeries.Empty);
        var forecasts = ModelForecasts.Empty.Update(forecast);

        var matrix = ConsensusStatistics.BuildAvailabilityMatrix(forecasts, t0.AddHours(3), "lucerne");

        Assert.True(matrix[IconD2]);
    }

    [Fact]
    public void Availability_model_beyond_horizon()
    {
        var t0 = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        var temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
        var point = new ForecastPoint(t0.AddHours(3), new Dictionary<ParameterDef, ForecastValue> { [temperature] = 22.5 });
        var forecast = new WeatherForecast(IconD2, "lucerne", new CycleId(t0),
            new ForecastSeries([point]), DailyForecastSeries.Empty);
        var forecasts = ModelForecasts.Empty.Update(forecast);

        var matrix = ConsensusStatistics.BuildAvailabilityMatrix(forecasts, t0.AddHours(72), "lucerne");

        Assert.False(matrix[IconD2]);
    }

    // --- mutation-hardening ---

    [Fact]
    public void Trimmed_mean_with_exactly_three_values_still_trims()
    {
        double?[] values = [1, 2, 9];
        Assert.Equal(2.0, ConsensusStatistics.ComputeTrimmedMean(values, 0.4));
    }

    [Fact]
    public void Trimmed_mean_drops_the_extremes_from_both_ends()
    {
        double?[] values = [1, 2, 3, 4, 5, 6, 7, 8, 9, 100];
        Assert.Equal(5.5, ConsensusStatistics.ComputeTrimmedMean(values, 0.2));
    }

    [Fact]
    public void Trimmed_mean_falls_back_to_full_mean_when_trimming_removes_everything()
    {
        double?[] values = [1, 2, 3, 10];
        Assert.Equal(4.0, ConsensusStatistics.ComputeTrimmedMean(values, 0.5));
    }

    [Fact]
    public void Trimmed_mean_all_null_returns_null()
    {
        double?[] values = [null, null];
        Assert.Null(ConsensusStatistics.ComputeTrimmedMean(values, 0.1));
    }

    [Fact]
    public void Spread_of_exactly_two_values_is_their_difference()
    {
        double?[] values = [15.0, 10.0];
        Assert.Equal(5.0, ConsensusStatistics.ComputeSpread(values));
    }

    [Fact]
    public void Iqr_of_exactly_four_values_interpolates_the_quartiles()
    {
        double?[] values = [0, 10, 20, 30];
        Assert.Equal(15.0, ConsensusStatistics.ComputeIqr(values));
    }

    [Fact]
    public void Iqr_with_integer_quartile_ranks_uses_the_exact_elements()
    {
        double?[] values = [1, 2, 4, 8, 16];
        Assert.Equal(6.0, ConsensusStatistics.ComputeIqr(values));
    }

    [Fact]
    public void Agreement_counts_a_value_exactly_at_the_tolerance()
    {
        double?[] values = [20.0, 22.0, 30.0];
        Assert.Equal(2.0 / 3.0, ConsensusStatistics.ComputeAgreement(values, 21.0, 1.0));
    }

    [Fact]
    public void Confidence_interval_of_exactly_two_values_interpolates_between_them()
    {
        double?[] values = [20.0, 10.0];
        var result = ConsensusStatistics.ComputeConfidenceInterval(values, 10, 90);
        Assert.NotNull(result);
        Assert.Equal(11.0, result.Value.Lower, 10);
        Assert.Equal(19.0, result.Value.Upper, 10);
    }

    [Fact]
    public void Confidence_interval_interpolates_by_fraction_of_the_gap()
    {
        double?[] values = [0, 100, 300];
        var result = ConsensusStatistics.ComputeConfidenceInterval(values, 25, 75);
        Assert.NotNull(result);
        Assert.Equal(50.0, result.Value.Lower, 10);
        Assert.Equal(200.0, result.Value.Upper, 10);
    }

    [Fact]
    public void Availability_ignores_models_of_other_locations()
    {
        var t0 = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        var temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
        WeatherForecast At(WeatherModel model, string location) => new(model, location, new CycleId(t0),
            new ForecastSeries([new ForecastPoint(t0, new Dictionary<ParameterDef, ForecastValue> { [temperature] = 1.0 })]),
            DailyForecastSeries.Empty);
        var forecasts = ModelForecasts.Empty.Update(At(IconD2, "lucerne")).Update(At(Ecmwf, "zurich"));

        var matrix = ConsensusStatistics.BuildAvailabilityMatrix(forecasts, t0, "lucerne");

        var entry = Assert.Single(matrix);
        Assert.Equal(IconD2, entry.Key);
        Assert.True(entry.Value);
    }

    [Fact]
    public void Availability_needs_only_one_point_near_the_target_time()
    {
        var t0 = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        var temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
        var values = new Dictionary<ParameterDef, ForecastValue> { [temperature] = 1.0 };
        var forecast = new WeatherForecast(IconD2, "lucerne", new CycleId(t0),
            new ForecastSeries([new ForecastPoint(t0, values), new ForecastPoint(t0.AddHours(5), values)]),
            DailyForecastSeries.Empty);
        var forecasts = ModelForecasts.Empty.Update(forecast);

        var matrix = ConsensusStatistics.BuildAvailabilityMatrix(forecasts, t0, "lucerne");

        Assert.True(matrix[IconD2]);
    }

    [Fact]
    public void Availability_treats_a_point_exactly_thirty_minutes_away_as_missing()
    {
        var t0 = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        var temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
        var point = new ForecastPoint(t0.AddMinutes(30), new Dictionary<ParameterDef, ForecastValue> { [temperature] = 1.0 });
        var forecast = new WeatherForecast(IconD2, "lucerne", new CycleId(t0),
            new ForecastSeries([point]), DailyForecastSeries.Empty);
        var forecasts = ModelForecasts.Empty.Update(forecast);

        var matrix = ConsensusStatistics.BuildAvailabilityMatrix(forecasts, t0, "lucerne");

        Assert.False(matrix[IconD2]);
    }
}
