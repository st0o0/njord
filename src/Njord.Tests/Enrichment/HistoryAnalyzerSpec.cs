using Njord.Domain;
using Njord.Enrichment;

namespace Njord.Tests.Enrichment;

public sealed class HistoryAnalyzerSpec
{
    private static readonly WeatherModel M1 = new("m1");
    private static readonly WeatherModel M2 = new("m2");

    private static ForecastHistory BuildHistory(int records, Func<int, (double m1Val, double m2Val, double consensus)> generator)
    {
        var history = new ForecastHistory(365);
        var baseTime = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

        for (var i = 0; i < records; i++)
        {
            var (m1Val, m2Val, consensus) = generator(i);
            history.Add(new ForecastRecord(
                baseTime.AddHours(i),
                "lucerne",
                new Dictionary<WeatherModel, IReadOnlyDictionary<string, double?>>
                {
                    [M1] = new Dictionary<string, double?> { ["temperature_2m"] = m1Val },
                    [M2] = new Dictionary<string, double?> { ["temperature_2m"] = m2Val },
                },
                new Dictionary<string, double?> { ["temperature_2m"] = consensus }));
        }
        return history;
    }

    // --- ModelAccuracy ---

    [Fact(Timeout = 5000)]
    public void ModelAccuracy_consistent_overshoot()
    {
        var history = BuildHistory(100, i => (22.0, 20.0, 20.0));
        var mae = HistoryAnalyzer.ModelAccuracy(history, "temperature_2m", 30);

        Assert.Equal(2.0, mae[M1]);
        Assert.Equal(0.0, mae[M2]);
    }

    [Fact(Timeout = 5000)]
    public void ModelAccuracy_insufficient_history()
    {
        var history = BuildHistory(10, i => (22.0, 20.0, 20.0));
        var mae = HistoryAnalyzer.ModelAccuracy(history, "temperature_2m", 30);

        Assert.Null(mae[M1]);
    }

    // --- ModelWeights ---

    [Fact(Timeout = 5000)]
    public void ModelWeights_low_error_gets_higher_weight()
    {
        var mae = new Dictionary<WeatherModel, double?> { [M1] = 0.5, [M2] = 2.0 };
        var weights = HistoryAnalyzer.ModelWeights(mae);

        Assert.True(weights[M1] > weights[M2]);
        Assert.InRange(weights.Values.Sum(), 0.99, 1.01);
    }

    [Fact(Timeout = 5000)]
    public void ModelWeights_all_null_gives_equal()
    {
        var mae = new Dictionary<WeatherModel, double?> { [M1] = null, [M2] = null };
        var weights = HistoryAnalyzer.ModelWeights(mae);

        Assert.Equal(weights[M1], weights[M2]);
    }

    // --- WeightedConsensus ---

    [Fact(Timeout = 5000)]
    public void WeightedConsensus_applies_weights()
    {
        var values = new (WeatherModel, double?)[] { (M1, 20.0), (M2, 24.0) };
        var weights = new Dictionary<WeatherModel, double> { [M1] = 0.75, [M2] = 0.25 };

        var result = HistoryAnalyzer.WeightedConsensus(values, weights);
        Assert.NotNull(result);
        Assert.Equal(21.0, result.Value);
    }

    // --- ForecastDrift ---

    [Fact(Timeout = 5000)]
    public void ForecastDrift_stable_model()
    {
        var history = BuildHistory(10, i => (20.0 + (i % 2) * 0.1, 20.0, 20.0));
        var drift = HistoryAnalyzer.ForecastDrift(history, M1, "temperature_2m", 5);

        Assert.NotNull(drift);
        Assert.InRange(drift.Value, 0, 0.2);
    }

    [Fact(Timeout = 5000)]
    public void ForecastDrift_unstable_model()
    {
        var history = BuildHistory(10, i => (15.0 + i * 2, 20.0, 20.0));
        var drift = HistoryAnalyzer.ForecastDrift(history, M1, "temperature_2m", 5);

        Assert.NotNull(drift);
        Assert.True(drift.Value > 2.0);
    }

    [Fact(Timeout = 5000)]
    public void ForecastDrift_insufficient_runs()
    {
        var history = BuildHistory(1, i => (20.0, 20.0, 20.0));
        Assert.Null(HistoryAnalyzer.ForecastDrift(history, M1, "temperature_2m"));
    }

    // --- SeasonalPreference ---

    [Fact(Timeout = 5000)]
    public void SeasonalPreference_best_model_in_summer()
    {
        var history = BuildHistory(100, i => (22.0, 20.0, 20.0));
        var best = HistoryAnalyzer.SeasonalPreference(history, "temperature_2m",
            new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(M2, best);
    }

    [Fact(Timeout = 5000)]
    public void SeasonalPreference_no_data()
    {
        var history = new ForecastHistory(30);
        Assert.Null(HistoryAnalyzer.SeasonalPreference(history, "temperature_2m",
            new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero)));
    }

    // --- AnomalyDetection ---

    [Fact(Timeout = 5000)]
    public void AnomalyDetection_normal_value()
    {
        var history = BuildHistory(100, i => (20.0, 20.0, 20.0));
        var result = HistoryAnalyzer.AnomalyDetection(history, "temperature_2m", 20.0, 12, minRecords: 3);

        Assert.NotNull(result);
        Assert.False(result.Value.IsAnomaly);
    }

    [Fact(Timeout = 5000)]
    public void AnomalyDetection_anomalous_value()
    {
        // All consensus at hour 0 will be 20.0, so 50.0 is a clear anomaly
        var history = BuildHistory(100, i => (20.0, 20.0, 20.0));
        var result = HistoryAnalyzer.AnomalyDetection(history, "temperature_2m", 50.0, 0, minRecords: 3);

        Assert.NotNull(result);
        // With all values = 20, stdDev = 0, function returns (false, 0) for zero-variance
        // Use varied data instead
        var history2 = BuildHistory(100, i => (20.0 + i % 5, 20.0, 18.0 + i % 5));
        var result2 = HistoryAnalyzer.AnomalyDetection(history2, "temperature_2m", 50.0, 0, minRecords: 3);

        Assert.NotNull(result2);
        Assert.True(result2.Value.IsAnomaly);
        Assert.True(result2.Value.DeviationSigma > 2.0);
    }

    [Fact(Timeout = 5000)]
    public void AnomalyDetection_insufficient_history()
    {
        var history = BuildHistory(10, i => (20.0, 20.0, 20.0));
        Assert.Null(HistoryAnalyzer.AnomalyDetection(history, "temperature_2m", 20.0, 12));
    }
}
