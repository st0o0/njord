using System.Diagnostics.CodeAnalysis;
using Njord.Consensus.Querying;

namespace Njord.Messages.Alerts;

public sealed record SeverityThresholds(
    double Advisory,
    double Warning,
    double Severe,
    ScanDirection Direction)
{
    public static bool TryCreate(
        IReadOnlyList<double> values, ScanDirection direction, [NotNullWhen(true)] out SeverityThresholds? thresholds)
    {
        if (values.Count < 3)
        {
            thresholds = null;
            return false;
        }

        thresholds = new SeverityThresholds(values[0], values[1], values[2], direction);
        return true;
    }

    public (AlertSeverity Severity, double EffectiveThreshold) Classify(double value, bool strict = false)
    {
        if (Crosses(value, Severe, strict))
        {
            return (AlertSeverity.Red, Severe);
        }

        if (Crosses(value, Warning, strict))
        {
            return (AlertSeverity.Orange, Warning);
        }

        if (Crosses(value, Advisory, strict))
        {
            return (AlertSeverity.Yellow, Advisory);
        }

        return (AlertSeverity.None, Advisory);
    }

    public bool CrossesAdvisory(double value, bool strict = false) =>
        Crosses(value, Advisory, strict);

    private bool Crosses(double value, double threshold, bool strict) =>
        (Direction, strict) switch
        {
            (ScanDirection.Min, false) => value <= threshold,
            (ScanDirection.Min, true) => value < threshold,
            (ScanDirection.Max, false) => value >= threshold,
            _ => value > threshold,
        };
}
