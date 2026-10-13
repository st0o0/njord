using Njord.Weather;

namespace Njord.Compute.Analysis;

public sealed record HorizonExtremeResult(
    bool Found,
    double ExtremeValue,
    Horizon? ExtremeHorizon,
    int? EarliestQualifyingHour,
    IReadOnlyList<double> Agreements,
    double Peak,
    double Sum,
    int Count,
    int QualifyingCount);

public static class HorizonExtremeScanner
{
    public static HorizonExtremeResult FindExtreme(
        IReadOnlyDictionary<Horizon, HorizonConsensus> byHorizon,
        int maxHours,
        ScanDirection direction,
        Func<double, bool>? qualifies = null,
        HorizonKind kind = HorizonKind.Hourly,
        double peakSeed = double.MinValue)
    {
        var found = false;
        var extreme = 0.0;
        Horizon? extremeHorizon = null;
        int? earliest = null;
        var agreements = new List<double>();
        var peak = peakSeed;
        var sum = 0.0;
        var count = 0;
        var qualifyingCount = 0;

        foreach (var (index, horizon, hc, value) in WithMedian(byHorizon, maxHours, kind))
        {
            count++;
            sum += value;

            var isNewExtreme = !found
                || (direction == ScanDirection.Min ? value < extreme : value > extreme);
            if (isNewExtreme)
            {
                found = true;
                extreme = value;
                extremeHorizon = horizon;
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
            found, extreme, extremeHorizon, earliest, agreements, peak, sum, count, qualifyingCount);
    }

    internal static IEnumerable<(int Index, Horizon Horizon, HorizonConsensus Consensus)> InWindow(
        IReadOnlyDictionary<Horizon, HorizonConsensus> byHorizon, int maxIndex, HorizonKind kind = HorizonKind.Hourly)
    {
        foreach (var (horizon, hc) in byHorizon)
        {
            if (horizon.Kind != kind || horizon.Offset > maxIndex)
            {
                continue;
            }

            yield return (horizon.Offset, horizon, hc);
        }
    }

    internal static IEnumerable<(int Index, Horizon Horizon, HorizonConsensus Consensus, double Value)> WithMedian(
        IReadOnlyDictionary<Horizon, HorizonConsensus> byHorizon, int maxIndex, HorizonKind kind = HorizonKind.Hourly)
    {
        foreach (var (index, horizon, hc) in InWindow(byHorizon, maxIndex, kind))
        {
            if (hc.Median is { } value)
            {
                yield return (index, horizon, hc, value);
            }
        }
    }
}
