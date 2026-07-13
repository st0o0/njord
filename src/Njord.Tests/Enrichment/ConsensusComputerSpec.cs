using Njord.Domain;
using Njord.Enrichment;

namespace Njord.Tests.Enrichment;

public sealed class ConsensusComputerSpec
{
    private static readonly WeatherModel IconD2 = new("icon_d2");
    private static readonly WeatherModel Ecmwf = new("ecmwf_ifs025");
    private static readonly WeatherModel Gfs = new("gfs_seamless");

    // --- ComputeMedian ---

    [Fact(Timeout = 5000)]
    public void Median_of_odd_count_returns_middle()
    {
        double?[] values = [19.0, 20.0, 21.0, 22.0, 23.0];
        Assert.Equal(21.0, ConsensusComputer.ComputeMedian(values));
    }

    [Fact(Timeout = 5000)]
    public void Median_of_even_count_returns_average_of_two_middle()
    {
        double?[] values = [20.0, 21.0, 22.0, 23.0];
        Assert.Equal(21.5, ConsensusComputer.ComputeMedian(values));
    }

    [Fact(Timeout = 5000)]
    public void Median_skips_nulls()
    {
        double?[] values = [20.0, null, 22.0, null, 21.0, 23.0, 19.0];
        Assert.Equal(21.0, ConsensusComputer.ComputeMedian(values));
    }

    [Fact(Timeout = 5000)]
    public void Median_all_null_returns_null()
    {
        double?[] values = [null, null, null];
        Assert.Null(ConsensusComputer.ComputeMedian(values));
    }

    [Fact(Timeout = 5000)]
    public void Median_single_value()
    {
        double?[] values = [42.0];
        Assert.Equal(42.0, ConsensusComputer.ComputeMedian(values));
    }

    // --- ComputeTrimmedMean ---

    [Fact(Timeout = 5000)]
    public void Trimmed_mean_10_percent_on_8_values()
    {
        double?[] values = [10, 12, 14, 16, 18, 20, 22, 24];
        // floor(8 * 0.1) = 0, so no trimming → mean of all 8
        var result = ConsensusComputer.ComputeTrimmedMean(values, 0.1);
        Assert.Equal(17.0, result);
    }

    [Fact(Timeout = 5000)]
    public void Trimmed_mean_20_percent_on_10_values()
    {
        double?[] values = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        // floor(10 * 0.2) = 2, trim 2 from each end → [3,4,5,6,7,8], mean = 5.5
        var result = ConsensusComputer.ComputeTrimmedMean(values, 0.2);
        Assert.Equal(5.5, result);
    }

    [Fact(Timeout = 5000)]
    public void Trimmed_mean_fewer_than_3_falls_back_to_simple_mean()
    {
        double?[] values = [20.0, 22.0];
        Assert.Equal(21.0, ConsensusComputer.ComputeTrimmedMean(values, 0.2));
    }

    // --- ComputeSpread ---

    [Fact(Timeout = 5000)]
    public void Spread_normal()
    {
        double?[] values = [18.0, 22.0, 20.0, null, 25.0];
        Assert.Equal(7.0, ConsensusComputer.ComputeSpread(values));
    }

    [Fact(Timeout = 5000)]
    public void Spread_single_value_returns_null()
    {
        double?[] values = [20.0];
        Assert.Null(ConsensusComputer.ComputeSpread(values));
    }

    // --- ComputeIqr ---

    [Fact(Timeout = 5000)]
    public void Iqr_8_values()
    {
        double?[] values = [18, 19, 20, 21, 22, 23, 24, 25];
        var result = ConsensusComputer.ComputeIqr(values);
        Assert.NotNull(result);
        Assert.Equal(3.5, result!.Value, 1);
    }

    [Fact(Timeout = 5000)]
    public void Iqr_fewer_than_4_returns_null()
    {
        double?[] values = [18.0, 20.0, 22.0];
        Assert.Null(ConsensusComputer.ComputeIqr(values));
    }

    // --- ComputeAgreement ---

    [Fact(Timeout = 5000)]
    public void Agreement_all_within_tolerance()
    {
        double?[] values = [20.0, 20.5, 19.5, 20.2];
        Assert.Equal(1.0, ConsensusComputer.ComputeAgreement(values, 20.1, 1.0));
    }

    [Fact(Timeout = 5000)]
    public void Agreement_partial()
    {
        double?[] values = [20.0, 25.0, 19.0, 30.0];
        Assert.Equal(0.5, ConsensusComputer.ComputeAgreement(values, 22.5, 3.0));
    }

    [Fact(Timeout = 5000)]
    public void Agreement_empty_returns_null()
    {
        double?[] values = [null, null];
        Assert.Null(ConsensusComputer.ComputeAgreement(values, 20.0, 1.0));
    }

    // --- IdentifyOutlier ---

    [Fact(Timeout = 5000)]
    public void Outlier_clear_deviation()
    {
        var models = new List<(WeatherModel, double?)>
        {
            (IconD2, 20.0), (Ecmwf, 21.0), (Gfs, 35.0),
        };
        var result = ConsensusComputer.IdentifyOutlier(models, 21.0);
        Assert.NotNull(result);
        Assert.Equal(Gfs, result!.Value.Model);
        Assert.Equal(14.0, result.Value.Deviation);
    }

    [Fact(Timeout = 5000)]
    public void Outlier_all_equal()
    {
        var models = new List<(WeatherModel, double?)>
        {
            (IconD2, 20.0), (Ecmwf, 20.0),
        };
        var result = ConsensusComputer.IdentifyOutlier(models, 20.0);
        Assert.NotNull(result);
        Assert.Equal(0.0, result!.Value.Deviation);
    }

    // --- ComputeConfidenceInterval ---

    [Fact(Timeout = 5000)]
    public void Confidence_interval_p10_p90_on_8_values()
    {
        double?[] values = [18, 19, 20, 21, 22, 23, 24, 25];
        var result = ConsensusComputer.ComputeConfidenceInterval(values, 10, 90);
        Assert.NotNull(result);
        Assert.True(result!.Value.Lower < result.Value.Upper);
    }

    [Fact(Timeout = 5000)]
    public void Confidence_interval_single_value_returns_null()
    {
        double?[] values = [20.0];
        Assert.Null(ConsensusComputer.ComputeConfidenceInterval(values, 10, 90));
    }

    // --- BuildAvailabilityMatrix ---

    [Fact(Timeout = 5000)]
    public void Availability_model_with_data_at_horizon()
    {
        var t0 = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        var temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
        var point = new ForecastPoint(t0.AddHours(3), new Dictionary<ParameterDef, double?> { [temperature] = 22.5 });
        var forecast = new ModelForecast(IconD2, "lucerne", new CycleId(t0),
            new ForecastSeries([point]), DailyForecastSeries.Empty);
        var snapshot = ModelSnapshot.Empty.Update(forecast);

        var matrix = ConsensusComputer.BuildAvailabilityMatrix(snapshot, t0.AddHours(3), "lucerne");

        Assert.True(matrix[IconD2]);
    }

    [Fact(Timeout = 5000)]
    public void Availability_model_beyond_horizon()
    {
        var t0 = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        var temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
        var point = new ForecastPoint(t0.AddHours(3), new Dictionary<ParameterDef, double?> { [temperature] = 22.5 });
        var forecast = new ModelForecast(IconD2, "lucerne", new CycleId(t0),
            new ForecastSeries([point]), DailyForecastSeries.Empty);
        var snapshot = ModelSnapshot.Empty.Update(forecast);

        var matrix = ConsensusComputer.BuildAvailabilityMatrix(snapshot, t0.AddHours(72), "lucerne");

        Assert.False(matrix[IconD2]);
    }
}
