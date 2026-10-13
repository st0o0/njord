namespace Njord.Compute.Contracts;

public enum Stability
{
    Converging,
    Diverging,
    Stable,
}

public static class StabilityExtensions
{
    public static string ToWireName(this Stability stability) => stability switch
    {
        Stability.Converging => "converging",
        Stability.Diverging => "diverging",
        Stability.Stable => "stable",
        _ => throw new ArgumentOutOfRangeException(nameof(stability), stability, null),
    };
}
