using Njord.Consensus;
using Njord.Consensus.Querying;
using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.Alerts.Rules;

public sealed class WindChillRule : IAlertRule
{
    public AlertType Type => AlertType.WindChill;

    public Alert Evaluate(LocationConsensus consensus, AlertSettings settings)
    {
        var thresholds = settings.WindChill;
        if (consensus.Hourly.Find(ParameterRegistry.ApparentTemperature) is not { } apparent)
        {
            return Alert.None(Type);
        }

        var window = apparent.Next24Hours();
        if (window.Extreme(ScanDirection.Min) is not { } low)
        {
            return Alert.None(Type);
        }

        var windFactor = 0.0;
        if (consensus.Hourly.Find(ParameterRegistry.Temperature2m).MedianAt(low.Horizon) is { } temperature)
        {
            windFactor = Math.Round(temperature - low.Value, 1);
        }

        var confidence = window.Hits(value => thresholds.CrossesAdvisory(value)).RoundedAverage();
        var (severity, effectiveThreshold) = thresholds.Classify(low.Value);
        if (confidence == 0.0)
        {
            severity = AlertSeverity.None;
        }

        var exposureRisk = severity switch
        {
            AlertSeverity.Red => ExposureRisk.Frostbite10Min,
            AlertSeverity.Orange => ExposureRisk.Frostbite30Min,
            _ => (ExposureRisk?)null,
        };

        var details = new WindChillDetails(
            FeltTemperature: Math.Round(low.Value, 1),
            WindFactor: windFactor,
            ExposureRisk: exposureRisk);

        return new Alert(Type, severity, confidence, details,
            TriggerValue: Math.Round(low.Value, 1),
            Threshold: effectiveThreshold);
    }
}
