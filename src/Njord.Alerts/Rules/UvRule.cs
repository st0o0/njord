using Njord.Consensus;
using Njord.Consensus.Querying;
using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.Alerts.Rules;

public sealed class UvRule : IAlertRule
{
    public AlertType Type => AlertType.Uv;

    public Alert Evaluate(LocationConsensus consensus, AlertSettings settings)
    {
        var maxUv = 0.0;
        if (consensus.Hourly.Find(ParameterRegistry.UvIndex)?.Next24Hours().Extreme(ScanDirection.Max) is { } high)
        {
            maxUv = Math.Max(maxUv, high.Value);
        }

        maxUv = Math.Max(maxUv, consensus.DailyMax(ParameterRegistry.UvIndexMax) ?? maxUv);
        if (maxUv <= 0)
        {
            return Alert.None(Type);
        }

        var (level, severity, uvThreshold) = maxUv switch
        {
            >= 11 => (UvLevel.Extreme, AlertSeverity.Red, 11.0),
            >= 8 => (UvLevel.VeryHigh, AlertSeverity.Red, 8.0),
            >= 6 => (UvLevel.High, AlertSeverity.Orange, 6.0),
            >= 3 => (UvLevel.Moderate, AlertSeverity.Yellow, 3.0),
            _ => (UvLevel.Low, AlertSeverity.None, 0.0),
        };

        var rounded = Math.Round(maxUv, 1);
        return new Alert(Type, severity, 1.0, new UvDetails(Level: level, UvIndex: rounded),
            TriggerValue: rounded,
            Threshold: uvThreshold);
    }
}
