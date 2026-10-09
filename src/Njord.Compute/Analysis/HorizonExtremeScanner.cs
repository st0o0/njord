namespace Njord.Compute.Analysis;

public sealed record HorizonExtremeResult(
    bool Found,
    double ExtremeValue,
    string? ExtremeHorizonKey,
    int? EarliestQualifyingHour,
    IReadOnlyList<double> Agreements,
    double Peak,
    double Sum,
    int Count,
    int QualifyingCount);

public static class HorizonExtremeScanner
{
    public static HorizonExtremeResult FindExtreme(
        IReadOnlyDictionary<string, HorizonConsensus> byHorizon,
        int maxHours,
        ScanDirection direction,
        Func<double, bool>? qualifies = null,
        char prefix = 'h',
        double peakSeed = double.MinValue)
    {
        var found = false;
        var extreme = 0.0;
        string? extremeKey = null;
        int? earliest = null;
        var agreements = new List<double>();
        var peak = peakSeed;
        var sum = 0.0;
        var count = 0;
        var qualifyingCount = 0;

        foreach (var (index, key, hc, value) in WithMedian(byHorizon, maxHours, prefix))
        {
            count++;
            sum += value;

            var isNewExtreme = !found
                || (direction == ScanDirection.Min ? value < extreme : value > extreme);
            if (isNewExtreme)
            {
                found = true;
                extreme = value;
                extremeKey = key;
            }

            if (hc.ConfidenceInterval is { Upper: var upper } && upper > peak)
            {
                peak = upper;
            }
            else if (value > peak)
            {
                peak = value;
            }

            if (qualifies?.Invoke(value) == true)
            {
                qualifyingCount++;
                if (earliest is null || index < earliest)
                {
                    earliest = index;
                }

                if (hc.Agreement is { } agreement)
                {
                    agreements.Add(agreement);
                }
            }
        }

        return new HorizonExtremeResult(
            found, extreme, extremeKey, earliest, agreements, peak, sum, count, qualifyingCount);
    }

    internal static IEnumerable<(int Index, string Key, HorizonConsensus Consensus)> InWindow(
        IReadOnlyDictionary<string, HorizonConsensus> byHorizon, int maxIndex, char prefix = 'h')
    {
        foreach (var (key, hc) in byHorizon)
        {
            var index = ParseIndex(key, prefix);
            if (index is null || index > maxIndex)
            {
                continue;
            }

            yield return (index.Value, key, hc);
        }
    }

    internal static IEnumerable<(int Index, string Key, HorizonConsensus Consensus, double Value)> WithMedian(
        IReadOnlyDictionary<string, HorizonConsensus> byHorizon, int maxIndex, char prefix = 'h')
    {
        foreach (var (index, key, hc) in InWindow(byHorizon, maxIndex, prefix))
        {
            if (hc.Median is { } value)
            {
                yield return (index, key, hc, value);
            }
        }
    }

    private static int? ParseIndex(string horizonKey, char prefix)
    {
        if (horizonKey.Length > 1 && horizonKey[0] == prefix && int.TryParse(horizonKey.AsSpan(1), out var index))
        {
            return index;
        }

        return null;
    }
}
