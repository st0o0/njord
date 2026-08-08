using System.Diagnostics.Metrics;

namespace Njord.Diagnostics;

public sealed class NjordMetrics
{
    public static readonly NjordMetrics Instance = new();
    public Meter Meter { get; } = new("Njord");
    private NjordMetrics() { }
}
