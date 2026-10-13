using Njord.Compute.Consensus;
using Njord.Compute.Contracts;
using Njord.Weather;

namespace Njord.Compute.Querying;

public static class ConsensusQueries
{
    public static ParameterConsensus? Find(this IReadOnlyList<ParameterConsensus> parameters, ParameterDef parameter) =>
        parameters.FirstOrDefault(p => p.Parameter == parameter);

    public static ParameterConsensus? Find(this HourlyConsensus hourly, ParameterDef parameter) =>
        hourly.Parameters.Find(parameter);

    public static ParameterConsensus? Find(this DailyConsensus daily, ParameterDef parameter) =>
        daily.Parameters.Find(parameter);

    public static HorizonConsensus? At(this ParameterConsensus? series, Horizon horizon) =>
        series?.ByHorizon.GetValueOrDefault(horizon);

    public static double? MedianAt(this ParameterConsensus? series, Horizon horizon) =>
        series.At(horizon)?.Median;

    public static int ModelsAt(this ParameterConsensus series, Horizon horizon) =>
        series.At(horizon)?.AvailableModels.Count ?? 0;

    public static HorizonWindow Within(this ParameterConsensus series, Horizon last) => new(series, last);

    public static IEnumerable<HorizonHit> Values(this HorizonWindow window)
    {
        foreach (var (horizon, consensus) in window.Entries)
        {
            if (consensus.Median is { } value)
            {
                yield return new HorizonHit(value, horizon, consensus.Agreement, consensus.AvailableModels.Count);
            }
        }
    }

    public static IEnumerable<HorizonHit> Hits(this HorizonWindow window, Func<double, bool> qualifies) =>
        window.Values().Where(hit => qualifies(hit.Value));

    public static HorizonHit? Earliest(this HorizonWindow window, Func<double, bool> qualifies)
    {
        HorizonHit? earliest = null;
        foreach (var hit in window.Hits(qualifies))
        {
            if (earliest is null || hit.Horizon.Offset < earliest.Horizon.Offset)
            {
                earliest = hit;
            }
        }

        return earliest;
    }

    public static HorizonHit? Extreme(this HorizonWindow window, ScanDirection direction)
    {
        HorizonHit? extreme = null;
        foreach (var hit in window.Values())
        {
            if (extreme is null
                || (direction == ScanDirection.Min ? hit.Value < extreme.Value : hit.Value > extreme.Value))
            {
                extreme = hit;
            }
        }

        return extreme;
    }

    public static double Peak(this HorizonWindow window, double seed = double.MinValue)
    {
        var peak = seed;
        foreach (var (_, consensus) in window.Entries)
        {
            if (consensus.Median is not { } value)
            {
                continue;
            }

            if (consensus.ConfidenceInterval is { Upper: var upper } && upper > peak)
            {
                peak = upper;
            }
            else if (value > peak)
            {
                peak = value;
            }
        }

        return peak;
    }

    public static double AgreementAbove(this HorizonWindow window, double threshold) =>
        AverageOrZero(window.Entries
            .Where(e => e.Consensus.Median is { } value && value >= threshold)
            .Select(e => e.Consensus.Agreement));

    public static double AverageAgreement(this HorizonWindow window) =>
        AverageOrZero(window.Entries.Select(e => e.Consensus.Agreement));

    public static int MaxModelCount(this HorizonWindow window) =>
        window.Entries
            .Select(e => e.Consensus.AvailableModels.Count)
            .DefaultIfEmpty(0)
            .Max();

    private static double AverageOrZero(IEnumerable<double?> agreements)
    {
        var present = agreements.OfType<double>().ToList();
        return present.Count > 0 ? present.Average() : 0.0;
    }
}
