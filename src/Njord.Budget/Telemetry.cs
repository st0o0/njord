using System.Diagnostics.Metrics;

namespace Njord.Budget;

internal static class Telemetry
{
    public const string MeterName = "Njord.Budget";

    internal static readonly Meter Meter = new(MeterName);
}
