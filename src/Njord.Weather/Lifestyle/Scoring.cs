namespace Njord.Weather.Lifestyle;

internal static class Scoring
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
}
