using Njord.Consensus;
using Njord.Consensus.Querying;
using Njord.Messages.Alerts;
using Njord.Weather;

namespace Njord.Alerts.Rules;

public sealed class HumidityRule : IAlertRule
{
    public AlertType Type => AlertType.Humidity;

    public Alert Evaluate(LocationConsensus consensus, AlertSettings settings)
    {
        var thresholds = settings.Humidity;
        var dewPoint = consensus.Hourly.Find(ParameterRegistry.DewPoint2m);
        var isDay = consensus.Hourly.Find(ParameterRegistry.IsDay);
        if (dewPoint is null || isDay is null)
        {
            return Alert.None(Type);
        }

        var maxDewpoint = double.MinValue;
        var dayAgreements = new List<double>();
        var dayHoursCount = 0;

        foreach (var horizon in isDay.DaytimeHours())
        {
            dayHoursCount++;
            if (dewPoint.At(horizon) is not { Median: { } value } day)
            {
                continue;
            }

            if (value > maxDewpoint)
            {
                maxDewpoint = value;
            }

            if (thresholds.CrossesAdvisory(value) && day.Agreement is { } agreement)
            {
                dayAgreements.Add(agreement);
            }
        }

        if (dayHoursCount == 0 || maxDewpoint <= double.MinValue)
        {
            return Alert.None(Type);
        }

        var confidence = dayAgreements.Count > 0 ? Math.Round(dayAgreements.Average(), 3) : 0.0;
        var (severity, effectiveThreshold) = thresholds.Classify(maxDewpoint);
        var comfortLevel = severity switch
        {
            AlertSeverity.Red => HumidityComfort.Tropical,
            AlertSeverity.Orange => HumidityComfort.Oppressive,
            AlertSeverity.Yellow => HumidityComfort.Muggy,
            _ => (HumidityComfort?)null,
        };

        if (confidence == 0.0)
        {
            severity = AlertSeverity.None;
        }

        var details = new HumidityDetails(MaxDewpoint: Math.Round(maxDewpoint, 1), ComfortLevel: comfortLevel);

        return new Alert(Type, severity, confidence, details,
            TriggerValue: Math.Round(maxDewpoint, 1),
            Threshold: effectiveThreshold);
    }
}
