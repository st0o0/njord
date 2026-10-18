using Njord.Compute.Consensus;
using Njord.Compute.Contracts;
using Njord.Compute.Querying;
using Njord.Compute.Settings;
using Njord.Weather;

namespace Njord.Alerts.Rules;

public sealed class FrostRule : IAlertRule
{
    public AlertType Type => AlertType.Frost;

    public Alert Evaluate(LocationConsensus consensus, AlertSettings settings)
    {
        var thresholds = settings.Frost;
        if (consensus.Hourly.Find(ParameterRegistry.Temperature2m) is not { } temperature)
        {
            return Alert.None(Type);
        }

        var window = temperature.Next24Hours();
        if (window.Extreme(ScanDirection.Min) is not { } low)
        {
            return Alert.None(Type);
        }

        bool Freezing(double value) => thresholds.CrossesAdvisory(value);
        var earliest = window.Earliest(Freezing);
        var confidence = window.Hits(Freezing).RoundedAverage();

        var (severity, effectiveThreshold) = thresholds.Classify(low.Value);
        if (confidence == 0.0)
        {
            severity = AlertSeverity.None;
        }

        var details = new FrostDetails(
            ExpectedLow: Math.Round(low.Value, 1),
            EarliestFrost: earliest?.Horizon,
            ModelsAgreeing: earliest?.ModelCount ?? 0);

        return new Alert(Type, severity, confidence, details,
            TriggerValue: Math.Round(low.Value, 1),
            Threshold: effectiveThreshold,
            HoursUntil: earliest?.Horizon.Offset);
    }
}
