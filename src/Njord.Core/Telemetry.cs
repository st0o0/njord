using System.Diagnostics.Metrics;

namespace Njord.Core;

internal static class Telemetry
{
    public const string MeterName = "Njord.Core";

    internal static readonly Meter Meter = new(MeterName);
}
