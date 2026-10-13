using Njord.Compute.Consensus;
using Njord.Compute.Contracts;
using Njord.Weather;

namespace Njord.Compute.Analysis;

public static class TrendCalculator
{
    public static (TrendDirection Direction, double Delta)? Direction(
        double? prevMedian, double? currMedian, double threshold)
    {
        if (prevMedian is not { } prev || currMedian is not { } curr)
        {
            return null;
        }

        var delta = curr - prev;
        var direction = delta > threshold ? TrendDirection.Rising
            : delta < -threshold ? TrendDirection.Falling
            : TrendDirection.Stable;
        return (direction, Math.Round(delta, 2));
    }

    public static WeatherChangeResult? WeatherChange(int? prevCode, int? currCode)
    {
        if (prevCode is not { } prev || currCode is not { } curr)
        {
            return null;
        }

        var fromCat = WeatherCategoryExtensions.FromWmoCode(prev);
        var toCat = WeatherCategoryExtensions.FromWmoCode(curr);
        if (fromCat == toCat)
        {
            return null;
        }

        return new WeatherChangeResult(fromCat, toCat);
    }

    public static (int? StartsInHours, int? EndsInHours) PrecipitationTiming(
        ForecastSeries series, ParameterDef precipParam, DateTimeOffset now)
    {
        var cutoff = now.AddHours(24);
        int? first = null, last = null;

        foreach (var point in series.Points)
        {
            if (point.ValidAt < now || point.ValidAt > cutoff)
            {
                continue;
            }

            var val = point.Get(precipParam);
            if (val is not { } v || v <= 0)
            {
                continue;
            }

            var hours = (int)Math.Round((point.ValidAt - now).TotalHours);
            first ??= hours;
            last = hours;
        }

        return (first, last);
    }

    public static (int? MaxInHours, int? MinInHours) ExtremaTiming(
        ForecastSeries series, ParameterDef tempParam, DateTimeOffset now)
    {
        var cutoff = now.AddHours(24);
        double? maxVal = null, minVal = null;
        int? maxHours = null, minHours = null;
        var count = 0;

        foreach (var point in series.Points)
        {
            if (point.ValidAt < now || point.ValidAt > cutoff)
            {
                continue;
            }

            var val = point.Get(tempParam);
            if (val is not { } v)
            {
                continue;
            }

            count++;
            var hours = (int)Math.Round((point.ValidAt - now).TotalHours);

            if (maxVal is null || v > maxVal)
            {
                maxVal = v;
                maxHours = hours;
            }
            if (minVal is null || v < minVal)
            {
                minVal = v;
                minHours = hours;
            }
        }

        return count < 2 ? (null, null) : (maxHours, minHours);
    }

    public static (Stability Stability, double Ratio)? ConsensusStability(
        double? prevIqr, double? currIqr)
    {
        if (prevIqr is not { } prev || currIqr is not { } curr)
        {
            return null;
        }

        if (prev == 0.0)
        {
            return null;
        }

        var ratio = Math.Round(curr / prev, 2);
        var stability = ratio < 0.8 ? Stability.Converging
            : ratio > 1.2 ? Stability.Diverging
            : Stability.Stable;
        return (stability, ratio);
    }

    public static (double DecayRate, int? ReliableHours)? PredictabilityDecay(
        IReadOnlyList<(int HorizonHours, double? Spread)> spreads, double spreadThreshold = 3.0)
    {
        var points = new List<(double X, double Y)>();
        int? reliableHours = null;

        foreach (var (h, s) in spreads)
        {
            if (s is not { } val)
            {
                continue;
            }

            points.Add((h, val));
            if (reliableHours is null && val > spreadThreshold)
            {
                reliableHours = h;
            }
        }

        if (points.Count < 2)
        {
            return null;
        }

        var slope = LinearRegressionSlope(points);
        return (Math.Round(slope, 4), reliableHours);
    }

    private static double LinearRegressionSlope(List<(double X, double Y)> points)
    {
        var n = points.Count;
        double sumX = 0, sumY = 0, sumXy = 0, sumX2 = 0;
        foreach (var (x, y) in points)
        {
            sumX += x;
            sumY += y;
            sumXy += x * y;
            sumX2 += x * x;
        }
        var denom = n * sumX2 - sumX * sumX;
        return denom == 0 ? 0 : (n * sumXy - sumX * sumY) / denom;
    }
}
