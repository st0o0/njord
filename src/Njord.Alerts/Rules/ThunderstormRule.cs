using Njord.Consensus;
using Njord.Consensus.Querying;
using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.Alerts.Rules;

public sealed class ThunderstormRule : IAlertRule
{
    public AlertType Type => AlertType.Thunderstorm;

    public Alert Evaluate(LocationConsensus consensus, AlertSettings settings)
    {
        var cape = consensus.Hourly.Find(ParameterRegistry.Cape);
        var precipitation = consensus.Hourly.Find(ParameterRegistry.Precipitation);
        var gusts = consensus.Hourly.Find(ParameterRegistry.WindGusts10m);
        if (cape is null || precipitation is null || gusts is null)
        {
            return Alert.None(Type);
        }

        var meetAllCount = 0;
        var totalHorizons = 0;
        var maxCape = 0.0;
        foreach (var horizon in AlertWindow.HourlyHorizons)
        {
            if (cape.At(horizon) is not { } capeHc || precipitation.At(horizon) is not { } precipHc
                || gusts.At(horizon) is not { } gustHc)
            {
                continue;
            }

            totalHorizons++;
            if (capeHc.Median is not { } cv)
            {
                continue;
            }

            maxCape = Math.Max(maxCape, cv);
            if (precipHc.Median is { } pv && gustHc.Median is { } gv
                && cv > settings.CapeThreshold && pv > settings.ThunderstormPrecipThreshold
                && gv > settings.ThunderstormGustThreshold)
            {
                meetAllCount++;
            }
        }

        if (totalHorizons == 0)
        {
            return Alert.None(Type);
        }

        var confidence = (double)meetAllCount / totalHorizons;
        var severity = confidence switch
        {
            > 0.75 => AlertSeverity.Red,
            >= 0.5 => AlertSeverity.Orange,
            > 0 => AlertSeverity.Yellow,
            _ => AlertSeverity.None,
        };

        var details = new ThunderstormDetails(ModelsAgreeing: cape.Next24Hours().MaxModelCount());

        return new Alert(Type, severity, Math.Round(confidence, 3), details,
            TriggerValue: Math.Round(maxCape, 0),
            Threshold: settings.CapeThreshold);
    }
}
