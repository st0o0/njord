

namespace Njord.Features.Shared;

public static class Scoring
{
    public static double Ramp(double value, double zeroAt, double oneAt)
    {
        var fraction = (value - zeroAt) / (oneAt - zeroAt);
        return Math.Clamp(fraction, 0.0, 1.0);
    }

    public static double Band(double value, double zeroLow, double oneLow, double oneHigh, double zeroHigh) =>
        Math.Min(Ramp(value, zeroLow, oneLow), Ramp(value, zeroHigh, oneHigh));

    public static (int Start, int Length) LongestRun(IReadOnlyList<bool> flags)
    {
        var bestStart = 0;
        var bestLength = 0;
        var start = -1;
        for (var i = 0; i <= flags.Count; i++)
        {
            var on = i < flags.Count && flags[i];
            if (on && start < 0)
            {
                start = i;
            }
            else if (!on && start >= 0)
            {
                if (i - start > bestLength)
                {
                    bestStart = start;
                    bestLength = i - start;
                }

                start = -1;
            }
        }

        return (bestStart, bestLength);
    }

    public static (int Score, int Start, int Length) BestWindow(IReadOnlyList<double> scores, double goodScore)
    {
        var (start, length) = LongestRun([.. scores.Select(score => score >= goodScore)]);
        if (length == 0)
        {
            var best = scores.ToList().IndexOf(scores.Max());
            return ((int)Math.Round(scores[best]), best, 0);
        }

        return ((int)Math.Round(scores.Skip(start).Take(length).Average()), start, length);
    }
}
