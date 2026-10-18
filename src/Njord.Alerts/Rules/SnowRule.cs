using Njord.Consensus;
using Njord.Consensus.Querying;
using Njord.Messages.Alerts;
using Njord.Weather;

namespace Njord.Alerts.Rules;

public sealed class SnowRule : IAlertRule
{
    public AlertType Type => AlertType.Snow;

    public Alert Evaluate(LocationConsensus consensus, AlertSettings settings)
    {
        if (consensus.Hourly.Find(ParameterRegistry.Snowfall) is not { } snowfall)
        {
            return Alert.None(Type);
        }

        var freezingLevels = consensus.Hourly.Find(ParameterRegistry.FreezingLevelHeight)?.Next24Hours().Values()
            .Select(hit => (double?)hit.Value)
            .ToList() ?? [];

        var window = snowfall.Next24Hours();
        var hourlySum = window.Values().Sum(hit => hit.Value);
        var totalSum = Math.Max(hourlySum, consensus.DailyMax(ParameterRegistry.SnowfallSum) ?? double.MinValue);
        var confidence = window.Hits(value => value > 0).RoundedAverage();

        var (severity, snowThreshold) = totalSum switch
        {
            > 20 => (AlertSeverity.Red, 20.0),
            > 5 => (AlertSeverity.Orange, 5.0),
            > 0 => (AlertSeverity.Yellow, 0.0),
            _ => (AlertSeverity.None, 0.0),
        };
        if (confidence == 0.0)
        {
            severity = AlertSeverity.None;
        }

        var roundedSum = Math.Round(totalSum, 1);
        var details = new SnowDetails(
            ExpectedAccumulation: roundedSum,
            FreezingLevel: freezingLevels.Count > 0 ? Math.Round(ConsensusStatistics.ComputeMedian(freezingLevels) ?? 0, 0) : null,
            ModelsAgreeing: window.MaxModelCount());

        return new Alert(Type, severity, confidence, details,
            TriggerValue: roundedSum,
            Threshold: snowThreshold);
    }
}
