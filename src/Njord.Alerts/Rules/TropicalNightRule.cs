using Njord.Consensus;
using Njord.Consensus.Querying;
using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.Alerts.Rules;

public sealed class TropicalNightRule : IAlertRule
{
    public AlertType Type => AlertType.TropicalNight;

    public Alert Evaluate(LocationConsensus consensus, AlertSettings settings)
    {
        var thresholds = settings.TropicalNight;
        var temperature = consensus.Hourly.Find(ParameterRegistry.Temperature2m);
        var isDay = consensus.Hourly.Find(ParameterRegistry.IsDay);
        if (temperature is null || isDay is null)
        {
            return Alert.None(Type);
        }

        var nightMin = double.MaxValue;
        var hoursAbove = 0;
        var nightAgreements = new List<double>();
        var nightHoursCount = 0;

        foreach (var horizon in isDay.NighttimeHours())
        {
            nightHoursCount++;
            if (temperature.At(horizon) is not { Median: { } value } night)
            {
                continue;
            }

            if (value < nightMin)
            {
                nightMin = value;
            }

            if (thresholds.CrossesAdvisory(value, strict: true))
            {
                hoursAbove++;
                if (night.Agreement is { } agreement)
                {
                    nightAgreements.Add(agreement);
                }
            }
        }

        if (nightHoursCount == 0 || nightMin >= double.MaxValue)
        {
            return Alert.None(Type);
        }

        var confidence = nightAgreements.Count > 0 ? Math.Round(nightAgreements.Average(), 3) : 0.0;
        var (severity, effectiveThreshold) = thresholds.Classify(nightMin, strict: true);
        if (confidence == 0.0)
        {
            severity = AlertSeverity.None;
        }

        var details = new TropicalNightDetails(NightMinimum: Math.Round(nightMin, 1), HoursAbove20: hoursAbove);

        return new Alert(Type, severity, confidence, details,
            TriggerValue: Math.Round(nightMin, 1),
            Threshold: effectiveThreshold);
    }
}
