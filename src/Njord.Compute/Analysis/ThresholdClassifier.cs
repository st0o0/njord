namespace Njord.Compute.Analysis;

public static class ThresholdClassifier
{
    public static (AlertSeverity Severity, double EffectiveThreshold) Classify(
        double value, IReadOnlyList<double> thresholds, ScanDirection direction, bool strict = false)
    {
        if (Crosses(value, thresholds[2], direction, strict))
        {
            return (AlertSeverity.Red, thresholds[2]);
        }

        if (Crosses(value, thresholds[1], direction, strict))
        {
            return (AlertSeverity.Orange, thresholds[1]);
        }

        if (Crosses(value, thresholds[0], direction, strict))
        {
            return (AlertSeverity.Yellow, thresholds[0]);
        }

        return (AlertSeverity.None, thresholds[0]);
    }

    private static bool Crosses(double value, double threshold, ScanDirection direction, bool strict) =>
        (direction, strict) switch
        {
            (ScanDirection.Min, false) => value <= threshold,
            (ScanDirection.Min, true) => value < threshold,
            (ScanDirection.Max, false) => value >= threshold,
            _ => value > threshold,
        };
}
