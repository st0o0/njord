using Njord.Domain.Weather;

namespace Njord.Domain.Analysis;

public static class ConsensusComputer
{
    public static double? ComputeMedian(IReadOnlyList<double?> values)
    {
        var sorted = NonNull(values);
        if (sorted.Count == 0)
        {
            return null;
        }

        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 0
            ? (sorted[mid - 1] + sorted[mid]) / 2.0
            : sorted[mid];
    }

    public static double? ComputeTrimmedMean(IReadOnlyList<double?> values, double trimPercent)
    {
        var sorted = NonNull(values);
        if (sorted.Count == 0)
        {
            return null;
        }

        if (sorted.Count < 3)
        {
            return sorted.Average();
        }

        var trimCount = (int)Math.Floor(sorted.Count * trimPercent);
        var remaining = sorted.Skip(trimCount).Take(sorted.Count - 2 * trimCount).ToList();
        return remaining.Count == 0 ? sorted.Average() : remaining.Average();
    }

    public static double? ComputeSpread(IReadOnlyList<double?> values)
    {
        var sorted = NonNull(values);
        return sorted.Count < 2 ? null : sorted[^1] - sorted[0];
    }

    public static double? ComputeIqr(IReadOnlyList<double?> values)
    {
        var sorted = NonNull(values);
        if (sorted.Count < 4)
        {
            return null;
        }

        var q1 = Percentile(sorted, 25);
        var q3 = Percentile(sorted, 75);
        return q3 - q1;
    }

    public static double? ComputeAgreement(IReadOnlyList<double?> values, double reference, double tolerance)
    {
        var nonNull = values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        if (nonNull.Count == 0)
        {
            return null;
        }

        var within = nonNull.Count(v => Math.Abs(v - reference) <= tolerance);
        return (double)within / nonNull.Count;
    }

    public static (WeatherModel Model, double Deviation)? IdentifyOutlier(
        IReadOnlyList<(WeatherModel Model, double? Value)> models, double reference)
    {
        WeatherModel? worstModel = null;
        var worstDeviation = -1.0;

        foreach (var (model, value) in models)
        {
            if (value is not { } v)
            {
                continue;
            }

            var deviation = Math.Abs(v - reference);
            if (deviation > worstDeviation)
            {
                worstDeviation = deviation;
                worstModel = model;
            }
        }

        return worstModel is null ? null : (worstModel, worstDeviation);
    }

    public static (double Lower, double Upper)? ComputeConfidenceInterval(
        IReadOnlyList<double?> values, double lowerPct, double upperPct)
    {
        var sorted = NonNull(values);
        if (sorted.Count < 2)
        {
            return null;
        }

        return (Percentile(sorted, lowerPct), Percentile(sorted, upperPct));
    }

    public static Dictionary<WeatherModel, bool> BuildAvailabilityMatrix(
        ModelSnapshot snapshot, DateTimeOffset targetTime, string location)
    {
        var result = new Dictionary<WeatherModel, bool>();
        foreach (var (key, forecast) in snapshot.Entries)
        {
            if (key.Location != location)
            {
                continue;
            }

            var hasData = forecast.Hourly.Points.Any(p =>
                Math.Abs((p.ValidAt - targetTime).TotalMinutes) < 30 && p.HasAnyValue);
            result[key.Model] = hasData;
        }
        return result;
    }

    private static List<double> NonNull(IReadOnlyList<double?> values)
    {
        var list = new List<double>(values.Count);
        foreach (var v in values)
        {
            if (v.HasValue)
            {
                list.Add(v.Value);
            }
        }
        list.Sort();
        return list;
    }

    private static double Percentile(List<double> sorted, double percentile)
    {
        var n = sorted.Count;
        var rank = percentile / 100.0 * (n - 1);
        var lower = (int)Math.Floor(rank);
        var upper = (int)Math.Ceiling(rank);
        if (lower == upper)
        {
            return sorted[lower];
        }

        var fraction = rank - lower;
        return sorted[lower] + fraction * (sorted[upper] - sorted[lower]);
    }
}
