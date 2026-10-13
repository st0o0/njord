namespace Njord.Compute.Analysis;

public enum TrendDirection
{
    Rising,
    Falling,
    Stable,
}

public static class TrendDirectionExtensions
{
    public static string ToWireName(this TrendDirection direction) => direction switch
    {
        TrendDirection.Rising => "rising",
        TrendDirection.Falling => "falling",
        TrendDirection.Stable => "stable",
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
    };
}
