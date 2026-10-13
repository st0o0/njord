using Njord.Compute.Consensus;
using Njord.Compute.Contracts;
using Njord.Compute.Querying;
using Njord.Compute.Settings;
using Njord.Weather;

namespace Njord.Alerts.Rules;

public sealed class VisibilityRule : IAlertRule
{
    public AlertType Type => AlertType.Visibility;

    public Alert Evaluate(ConsensusSnapshot consensus, AlertSettings settings)
    {
        var thresholds = settings.Visibility;
        if (consensus.Hourly.Find(ParameterRegistry.Visibility) is not { } visibility)
        {
            return Alert.None(Type);
        }

        var window = visibility.Next24Hours();
        if (window.Extreme(ScanDirection.Min) is not { } low)
        {
            return Alert.None(Type);
        }

        var hoursBelow = window.Hits(value => thresholds.CrossesAdvisory(value, strict: true)).Count();
        var totalHorizons = window.Values().Count();
        var confidence = totalHorizons > 0 && hoursBelow > 0
            ? Math.Round((double)hoursBelow / totalHorizons, 3)
            : 0.0;

        var (severity, effectiveThreshold) = thresholds.Classify(low.Value, strict: true);
        var details = new VisibilityDetails(MinVisibility: Math.Round(low.Value, 0), HoursBelow1000m: hoursBelow);

        return new Alert(Type, severity, confidence, details,
            TriggerValue: Math.Round(low.Value, 0),
            Threshold: effectiveThreshold);
    }
}
