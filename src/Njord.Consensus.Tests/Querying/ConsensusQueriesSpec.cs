using Njord.Consensus.Querying;
using Njord.Model;

namespace Njord.Consensus.Tests.Querying;

public sealed class ConsensusQueriesSpec
{
    private static readonly Horizon Day = Horizon.Hours(24);

    private static HorizonConsensus Hc(double? median, double? agreement = null, double? upper = null, int models = 0) =>
        new(median, null, null, null, agreement, null,
            upper is null ? null : new ConfidenceIntervalInfo(median ?? 0, upper.Value),
            [.. Enumerable.Range(0, models).Select(i => new WeatherModel($"m{i}"))]);

    private static ParameterConsensus Series(params (Horizon Key, HorizonConsensus Hc)[] entries) =>
        new(ParameterRegistry.Temperature2m, entries.ToDictionary(e => e.Key, e => e.Hc));

    [Fact]
    public void Min_scan_finds_lowest_median_and_its_horizon_key()
    {
        var series = Series((Horizon.Hours(0), Hc(5)), (Horizon.Hours(1), Hc(-2)), (Horizon.Hours(2), Hc(3)));

        var hit = series.Within(Day).Extreme(ScanDirection.Min);

        Assert.NotNull(hit);
        Assert.Equal(-2, hit.Value);
        Assert.Equal(Horizon.Hours(1), hit.Horizon);
    }

    [Fact]
    public void Max_scan_finds_highest_median()
    {
        var series = Series((Horizon.Hours(0), Hc(5)), (Horizon.Hours(1), Hc(-2)), (Horizon.Hours(2), Hc(9)));

        var hit = series.Within(Day).Extreme(ScanDirection.Max);

        Assert.NotNull(hit);
        Assert.Equal(9, hit.Value);
        Assert.Equal(Horizon.Hours(2), hit.Horizon);
    }

    [Fact]
    public void Scan_ignores_horizons_beyond_the_window_and_missing_medians()
    {
        var series = Series((Horizon.Hours(0), Hc(null)), (Horizon.Hours(25), Hc(100)), (Horizon.Hours(3), Hc(4)), (Horizon.Days(1), Hc(50)));

        var window = series.Within(Day);

        Assert.Single(window.Values());
        Assert.Equal(4, window.Extreme(ScanDirection.Max)?.Value);
    }

    [Fact]
    public void Scan_without_usable_entries_reports_not_found()
    {
        var series = Series((Horizon.Hours(0), Hc(null)), (Horizon.Hours(30), Hc(1)));

        var window = series.Within(Day);

        Assert.Multiple(
            () => Assert.Null(window.Extreme(ScanDirection.Min)),
            () => Assert.Null(window.Earliest(_ => true)),
            () => Assert.Empty(window.Hits(_ => true)),
            () => Assert.Empty(window.Values()));
    }

    [Fact]
    public void Qualifying_entries_yield_earliest_hour_count_and_agreements()
    {
        var series = Series(
            (Horizon.Hours(6), Hc(-3, agreement: 0.8)),
            (Horizon.Hours(2), Hc(-1, agreement: 0.6)),
            (Horizon.Hours(4), Hc(5, agreement: 0.9)),
            (Horizon.Hours(9), Hc(-4)));

        var window = series.Within(Day);
        var hits = window.Hits(v => v <= 0).ToList();

        Assert.Multiple(
            () => Assert.Equal(Horizon.Hours(2), window.Earliest(v => v <= 0)?.Horizon),
            () => Assert.Equal(3, hits.Count),
            () => Assert.Equal([0.8, 0.6], hits.Select(h => h.Agreement).OfType<double>()));
    }

    [Fact]
    public void Peak_prefers_confidence_interval_upper_and_sum_adds_medians()
    {
        var series = Series((Horizon.Hours(0), Hc(10, upper: 14)), (Horizon.Hours(1), Hc(12)));

        var window = series.Within(Day);

        Assert.Equal(14, window.Peak());
        Assert.Equal(22, window.Values().Sum(h => h.Value));
    }

    [Fact]
    public void Peak_seed_carries_over_between_scans()
    {
        var series = Series((Horizon.Hours(0), Hc(3)));

        Assert.Equal(20, series.Within(Day).Peak(seed: 20));
    }

    [Fact]
    public void Daily_window_scans_daily_keys_only()
    {
        var series = Series((Horizon.Days(0), Hc(2)), (Horizon.Days(1), Hc(7)), (Horizon.Days(2), Hc(99)), (Horizon.Hours(0), Hc(500)));

        var window = series.Within(Horizon.Days(1));

        Assert.Equal(7, window.Extreme(ScanDirection.Max)?.Value);
        Assert.Equal(2, window.Values().Count());
    }

    [Fact]
    public void Extreme_reports_agreement_and_model_count_at_its_horizon()
    {
        var series = Series((Horizon.Hours(0), Hc(5, agreement: 0.5, models: 2)), (Horizon.Hours(1), Hc(8, agreement: 0.75, models: 3)));

        var hit = series.Within(Day).Extreme(ScanDirection.Max);

        Assert.NotNull(hit);
        Assert.Equal(0.75, hit.Agreement);
        Assert.Equal(3, hit.ModelCount);
    }

    [Fact]
    public void Agreement_above_averages_agreement_where_the_median_reaches_the_threshold()
    {
        var series = Series(
            (Horizon.Hours(0), Hc(29, agreement: 0.1)),
            (Horizon.Hours(1), Hc(30, agreement: 0.6)),
            (Horizon.Hours(2), Hc(32, agreement: 0.8)),
            (Horizon.Hours(3), Hc(35)),
            (Horizon.Hours(30), Hc(40, agreement: 0.0)));

        Assert.Equal(0.7, series.Within(Day).AgreementAbove(30), 10);
    }

    [Fact]
    public void Agreement_above_is_zero_without_matching_agreement()
    {
        var series = Series((Horizon.Hours(0), Hc(10, agreement: 0.9)));

        Assert.Equal(0.0, series.Within(Day).AgreementAbove(30));
    }

    [Fact]
    public void Average_agreement_covers_every_window_entry_with_an_agreement()
    {
        var series = Series(
            (Horizon.Hours(0), Hc(null, agreement: 0.2)),
            (Horizon.Hours(1), Hc(5, agreement: 0.6)),
            (Horizon.Hours(2), Hc(5)),
            (Horizon.Hours(25), Hc(5, agreement: 1.0)));

        Assert.Equal(0.4, series.Within(Day).AverageAgreement(), 10);
    }

    [Fact]
    public void Average_agreement_of_an_empty_window_is_zero()
    {
        var series = Series((Horizon.Hours(30), Hc(5, agreement: 1.0)));

        Assert.Equal(0.0, series.Within(Day).AverageAgreement());
    }

    [Fact]
    public void Max_model_count_includes_entries_without_median()
    {
        var series = Series(
            (Horizon.Hours(0), Hc(null, models: 4)),
            (Horizon.Hours(1), Hc(5, models: 2)),
            (Horizon.Hours(25), Hc(5, models: 9)));

        Assert.Equal(4, series.Within(Day).MaxModelCount());
    }

    [Fact]
    public void Max_model_count_of_an_empty_window_is_zero()
    {
        var series = Series((Horizon.Hours(30), Hc(5, models: 3)));

        Assert.Equal(0, series.Within(Day).MaxModelCount());
    }

    [Fact]
    public void Models_at_counts_available_models_at_one_horizon()
    {
        var series = Series((Horizon.Hours(0), Hc(5, models: 3)));

        Assert.Multiple(
            () => Assert.Equal(3, series.ModelsAt(Horizon.Hours(0))),
            () => Assert.Equal(0, series.ModelsAt(Horizon.Hours(1))));
    }

    [Fact]
    public void Median_at_returns_the_median_at_one_horizon()
    {
        var series = Series((Horizon.Hours(0), Hc(5)), (Horizon.Hours(1), Hc(null)));

        Assert.Multiple(
            () => Assert.Equal(5, series.MedianAt(Horizon.Hours(0))),
            () => Assert.Null(series.MedianAt(Horizon.Hours(1))),
            () => Assert.Null(series.MedianAt(Horizon.Hours(2))),
            () => Assert.Null(((ParameterConsensus?)null).MedianAt(Horizon.Hours(0))));
    }

    [Fact]
    public void Find_returns_the_series_of_a_parameter_or_null()
    {
        var temperature = Series((Horizon.Hours(0), Hc(5)));
        IReadOnlyList<ParameterConsensus> parameters = [temperature];

        Assert.Multiple(
            () => Assert.Same(temperature, parameters.Find(ParameterRegistry.Temperature2m)),
            () => Assert.Null(parameters.Find(ParameterRegistry.WindSpeed10m)));
    }

    [Fact]
    public void Find_works_on_hourly_and_daily_consensus()
    {
        var temperature = Series((Horizon.Hours(0), Hc(5)));
        var hourly = new HourlyConsensus([temperature], 0);
        var daily = new DailyConsensus([], 0);

        Assert.Multiple(
            () => Assert.Same(temperature, hourly.Find(ParameterRegistry.Temperature2m)),
            () => Assert.Null(daily.Find(ParameterRegistry.Temperature2m)));
    }

    [Fact]
    public void Min_scan_keeps_the_first_of_equal_medians()
    {
        var series = Series((Horizon.Hours(0), Hc(-2)), (Horizon.Hours(1), Hc(-2)), (Horizon.Hours(2), Hc(4)));

        var hit = series.Within(Day).Extreme(ScanDirection.Min);

        Assert.NotNull(hit);
        Assert.Equal(Horizon.Hours(0), hit.Horizon);
    }

    [Fact]
    public void Max_scan_keeps_the_first_of_equal_medians()
    {
        var series = Series((Horizon.Hours(0), Hc(9)), (Horizon.Hours(1), Hc(9)), (Horizon.Hours(2), Hc(4)));

        var hit = series.Within(Day).Extreme(ScanDirection.Max);

        Assert.NotNull(hit);
        Assert.Equal(Horizon.Hours(0), hit.Horizon);
    }
}
