using System.Diagnostics.Metrics;

namespace Njord.Budget;

internal static class Telemetry
{
    internal static readonly Meter Meter = new("Njord.Budget");
}
