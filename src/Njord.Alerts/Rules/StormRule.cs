using Njord.Compute.Consensus;
using Njord.Compute.Contracts;
using Njord.Compute.Querying;
using Njord.Compute.Settings;
using Njord.Weather;

namespace Njord.Alerts.Rules;

public sealed class StormRule : IAlertRule
{
    public AlertType Type => AlertType.Storm;

    public Alert Evaluate(LocationConsensus consensus, AlertSettings settings)
    {
        var thresholds = settings.StormGust;
        if (consensus.Hourly.Find(ParameterRegistry.WindGusts10m) is not { } gusts)
        {
            return Alert.None(Type);
        }

        var window = gusts.Next24Hours();
        if (window.Extreme(ScanDirection.Max) is not { } high)
        {
            return Alert.None(Type);
        }

        var confidence = window.Hits(value => thresholds.CrossesAdvisory(value)).RoundedAverage();
        var (severity, effectiveThreshold) = thresholds.Classify(high.Value);
        if (confidence == 0.0)
        {
            severity = AlertSeverity.None;
        }

        var peak = window.Peak();
        var medianRounded = Math.Round(high.Value, 1);
        var peakRounded = peak > double.MinValue ? Math.Round(peak, 1) : medianRounded;
        var details = new StormDetails(ExpectedMaxGust: medianRounded, ModelsAgreeing: window.MaxModelCount());

        return new Alert(Type, severity, confidence, details,
            TriggerValue: medianRounded,
            Threshold: effectiveThreshold,
            PeakValue: peakRounded != medianRounded ? peakRounded : null);
    }
}
