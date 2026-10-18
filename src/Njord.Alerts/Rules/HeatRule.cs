using Njord.Consensus;
using Njord.Consensus.Querying;
using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.Alerts.Rules;

public sealed class HeatRule : IAlertRule
{
    public AlertType Type => AlertType.Heat;

    public Alert Evaluate(LocationConsensus consensus, AlertSettings settings)
    {
        var apparent = consensus.Hourly.Find(ParameterRegistry.ApparentTemperature);
        var temperature = consensus.Hourly.Find(ParameterRegistry.Temperature2m);
        if ((apparent ?? temperature) is not { } best)
        {
            return Alert.None(Type);
        }

        var found = false;
        var maxMedian = double.MinValue;
        var peak = double.MinValue;
        foreach (var source in new[] { apparent, temperature }.OfType<ParameterConsensus>())
        {
            var window = source.Next24Hours();
            peak = window.Peak(peak);
            if (window.Extreme(ScanDirection.Max) is { } high && high.Value > maxMedian)
            {
                found = true;
                maxMedian = high.Value;
                best = source;
            }
        }

        if (!found)
        {
            return Alert.None(Type);
        }

        var (severity, effectiveThreshold) = settings.Heat.Classify(maxMedian);
        if (severity == AlertSeverity.None)
        {
            return Alert.None(Type);
        }

        var bestWindow = best.Next24Hours();
        var medianRounded = Math.Round(maxMedian, 1);
        var peakRounded = peak > double.MinValue ? Math.Round(peak, 1) : medianRounded;
        var details = new HeatDetails(ExpectedMax: medianRounded, ModelsAgreeing: bestWindow.MaxModelCount());

        return new Alert(Type, severity, Math.Round(bestWindow.AgreementAbove(effectiveThreshold), 3), details,
            TriggerValue: medianRounded,
            Threshold: effectiveThreshold,
            PeakValue: peakRounded != medianRounded ? peakRounded : null);
    }
}
