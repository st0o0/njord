using Njord.Compute.Analysis;
using Njord.Compute.Consensus;
using Njord.Compute.Contracts;
using Njord.Weather;

namespace Njord.Compute.Tests.Analysis;

public sealed class HorizonExtremeScannerSpec
{
    private static HorizonConsensus Hc(double? median, double? agreement = null, double? upper = null) =>
        new(median, null, null, null, agreement, null,
            upper is null ? null : new ConfidenceIntervalInfo(median ?? 0, upper.Value),
            []);

    private static Dictionary<Horizon, HorizonConsensus> Series(params (Horizon Key, HorizonConsensus Hc)[] entries) =>
        entries.ToDictionary(e => e.Key, e => e.Hc);

    [Fact]
    public void Min_scan_finds_lowest_median_and_its_horizon_key()
    {
        var byHorizon = Series((Horizon.Hours(0), Hc(5)), (Horizon.Hours(1), Hc(-2)), (Horizon.Hours(2), Hc(3)));

        var result = HorizonExtremeScanner.FindExtreme(byHorizon, 24, ScanDirection.Min);

        Assert.True(result.Found);
        Assert.Equal(-2, result.ExtremeValue);
        Assert.Equal(Horizon.Hours(1), result.ExtremeHorizon);
    }

    [Fact]
    public void Max_scan_finds_highest_median()
    {
        var byHorizon = Series((Horizon.Hours(0), Hc(5)), (Horizon.Hours(1), Hc(-2)), (Horizon.Hours(2), Hc(9)));

        var result = HorizonExtremeScanner.FindExtreme(byHorizon, 24, ScanDirection.Max);

        Assert.True(result.Found);
        Assert.Equal(9, result.ExtremeValue);
        Assert.Equal(Horizon.Hours(2), result.ExtremeHorizon);
    }

    [Fact]
    public void Scan_ignores_horizons_beyond_max_hours_and_missing_medians()
    {
        var byHorizon = Series((Horizon.Hours(0), Hc(null)), (Horizon.Hours(25), Hc(100)), (Horizon.Hours(3), Hc(4)), (Horizon.Days(1), Hc(50)));

        var result = HorizonExtremeScanner.FindExtreme(byHorizon, 24, ScanDirection.Max);

        Assert.Equal(1, result.Count);
        Assert.Equal(4, result.ExtremeValue);
    }

    [Fact]
    public void Scan_without_usable_entries_reports_not_found()
    {
        var byHorizon = Series((Horizon.Hours(0), Hc(null)), (Horizon.Hours(30), Hc(1)));

        var result = HorizonExtremeScanner.FindExtreme(byHorizon, 24, ScanDirection.Min);

        Assert.False(result.Found);
        Assert.Null(result.ExtremeHorizon);
        Assert.Null(result.EarliestQualifyingHour);
        Assert.Empty(result.Agreements);
        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void Qualifying_entries_yield_earliest_hour_count_and_agreements()
    {
        var byHorizon = Series(
            (Horizon.Hours(6), Hc(-3, agreement: 0.8)),
            (Horizon.Hours(2), Hc(-1, agreement: 0.6)),
            (Horizon.Hours(4), Hc(5, agreement: 0.9)),
            (Horizon.Hours(9), Hc(-4)));

        var result = HorizonExtremeScanner.FindExtreme(byHorizon, 24, ScanDirection.Min, v => v <= 0);

        Assert.Multiple(
            () => Assert.Equal(2, result.EarliestQualifyingHour),
            () => Assert.Equal(3, result.QualifyingCount),
            () => Assert.Equal([0.8, 0.6], result.Agreements));
    }

    [Fact]
    public void Peak_prefers_confidence_interval_upper_and_sum_adds_medians()
    {
        var byHorizon = Series((Horizon.Hours(0), Hc(10, upper: 14)), (Horizon.Hours(1), Hc(12)));

        var result = HorizonExtremeScanner.FindExtreme(byHorizon, 24, ScanDirection.Max);

        Assert.Equal(14, result.Peak);
        Assert.Equal(22, result.Sum);
    }

    [Fact]
    public void Peak_seed_carries_over_between_scans()
    {
        var byHorizon = Series((Horizon.Hours(0), Hc(3)));

        var result = HorizonExtremeScanner.FindExtreme(byHorizon, 24, ScanDirection.Max, peakSeed: 20);

        Assert.Equal(20, result.Peak);
    }

    [Fact]
    public void Day_prefix_scans_daily_keys_only()
    {
        var byHorizon = Series((Horizon.Days(0), Hc(2)), (Horizon.Days(1), Hc(7)), (Horizon.Days(2), Hc(99)), (Horizon.Hours(0), Hc(500)));

        var result = HorizonExtremeScanner.FindExtreme(byHorizon, 1, ScanDirection.Max, kind: HorizonKind.Daily);

        Assert.Equal(7, result.ExtremeValue);
        Assert.Equal(2, result.Count);
    }
}
