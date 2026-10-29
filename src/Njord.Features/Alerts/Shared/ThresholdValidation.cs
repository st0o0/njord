using Njord.Messages.Alerts;

namespace Njord.Features.Alerts.Shared;

internal static class ThresholdValidation
{
    internal static string? Violation(string path, double[]? values, ScanDirection direction)
    {
        if (values is null)
        {
            return null;
        }

        if (values.Length != 3)
        {
            return $"{path} needs exactly three values (advisory, warning, severe), got {values.Length}.";
        }

        if (!values.All(double.IsFinite))
        {
            return $"{path} must contain only finite numbers, got [{string.Join(", ", values)}].";
        }

        var ordered = direction == ScanDirection.Max
            ? values[0] < values[1] && values[1] < values[2]
            : values[0] > values[1] && values[1] > values[2];
        return ordered
            ? null
            : $"{path} must be {(direction == ScanDirection.Max ? "strictly ascending" : "strictly descending")} (advisory, warning, severe), got [{string.Join(", ", values)}].";
    }
}
