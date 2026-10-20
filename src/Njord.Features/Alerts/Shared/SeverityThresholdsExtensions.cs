using Njord.Messages.Alerts;

namespace Njord.Features.Alerts.Shared;

internal static class SeverityThresholdsExtensions
{
    internal static SeverityThresholds ToSeverity(this double[] values, ScanDirection direction) =>
        new(values[0], values[1], values[2], direction);
}
