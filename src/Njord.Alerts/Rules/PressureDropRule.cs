using Njord.Compute.Consensus;
using Njord.Compute.Contracts;
using Njord.Compute.Querying;
using Njord.Compute.Settings;
using Njord.Weather;

namespace Njord.Alerts.Rules;

public sealed class PressureDropRule : IAlertRule
{
    private const int DropSpanHours = 3;
    private const int MinimumHours = 4;

    public AlertType Type => AlertType.PressureDrop;

    public Alert Evaluate(ConsensusSnapshot consensus, AlertSettings settings)
    {
        if (consensus.Hourly.Find(ParameterRegistry.PressureMsl) is not { } pressure)
        {
            return Alert.None(Type);
        }

        var window = pressure.Next24Hours();
        var medianByHour = new SortedDictionary<int, double>();
        foreach (var hit in window.Values())
        {
            medianByHour[hit.Horizon.Offset] = hit.Value;
        }

        if (medianByHour.Count < MinimumHours)
        {
            return Alert.None(Type);
        }

        var maxDrop = MaxDrop(medianByHour);
        var dropThreshold = settings.PressureDropThreshold;
        var severeThreshold = settings.PressureDropSevereThreshold;
        var exceeds = maxDrop >= dropThreshold;
        var confidence = exceeds ? window.AverageAgreement() : 0.0;

        var severity = maxDrop switch
        {
            _ when maxDrop >= severeThreshold => AlertSeverity.Orange,
            _ when maxDrop >= dropThreshold => AlertSeverity.Yellow,
            _ => AlertSeverity.None,
        };

        var medianDrop = Math.Round(maxDrop, 1);
        var details = new PressureDropDetails(MaxDrop: medianDrop, ModelsAgreeing: window.MaxModelCount());

        return new Alert(Type, severity, Math.Round(confidence, 3), details,
            TriggerValue: medianDrop,
            Threshold: exceeds && maxDrop >= severeThreshold ? severeThreshold : dropThreshold);
    }

    private static double MaxDrop(SortedDictionary<int, double> medianByHour)
    {
        var maxDrop = 0.0;
        var hours = medianByHour.Keys.ToList();
        for (var i = 0; i < hours.Count; i++)
        {
            for (var j = 0; j < i; j++)
            {
                if (hours[i] - hours[j] >= DropSpanHours)
                {
                    maxDrop = Math.Max(maxDrop, medianByHour[hours[j]] - medianByHour[hours[i]]);
                }
            }
        }

        return maxDrop;
    }
}
