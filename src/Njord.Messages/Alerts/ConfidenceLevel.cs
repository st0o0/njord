namespace Njord.Messages.Alerts;

public enum ConfidenceLevel
{
    Low,
    Medium,
    High,
}

public sealed record ConfidenceThresholds(double HighBelowStdDev, double LowAboveStdDev)
{
    public static readonly ConfidenceThresholds Default = new(1.0, 3.0);

    public ConfidenceLevel LevelFor(double? stdDev) => stdDev switch
    {
        null => ConfidenceLevel.Medium,
        var s when s < HighBelowStdDev => ConfidenceLevel.High,
        var s when s > LowAboveStdDev => ConfidenceLevel.Low,
        _ => ConfidenceLevel.Medium,
    };
}
