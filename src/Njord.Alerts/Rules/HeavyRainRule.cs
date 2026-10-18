using Njord.Consensus;
using Njord.Consensus.Querying;
using Njord.Messages.Alerts;
using Njord.Weather;

namespace Njord.Alerts.Rules;

public sealed class HeavyRainRule : IAlertRule
{
    public AlertType Type => AlertType.HeavyRain;

    public Alert Evaluate(LocationConsensus consensus, AlertSettings settings)
    {
        if (consensus.Hourly.Find(ParameterRegistry.Precipitation) is not { } precipitation)
        {
            return Alert.None(Type);
        }

        var hourlyThreshold = settings.HeavyRainHourlyThreshold;
        var dailyThreshold = settings.HeavyRainDailyThreshold;
        var window = precipitation.Next24Hours();
        var hourlyHits = window.Hits(value => value >= hourlyThreshold).ToList();
        var maxHourly = Math.Max(0.0, window.Extreme(ScanDirection.Max)?.Value ?? 0.0);
        var hourlySum = window.Values().Sum(hit => hit.Value);
        var dailySum = Math.Max(hourlySum, consensus.DailyMax(ParameterRegistry.PrecipitationSum) ?? double.MinValue);

        var dailyExceeds = dailySum >= dailyThreshold;
        var hourlyExceeds = hourlyHits.Count > 0;
        var severity = (hourlyExceeds, dailyExceeds) switch
        {
            (true, true) => AlertSeverity.Red,
            (false, true) => AlertSeverity.Orange,
            (true, false) => AlertSeverity.Yellow,
            _ => AlertSeverity.None,
        };

        var agreements = hourlyHits.Select(hit => hit.Agreement).OfType<double>().ToList();
        var confidence = agreements.Count > 0
            ? Math.Round(agreements.Average(), 3)
            : (dailyExceeds ? 1.0 : 0.0);

        var details = new HeavyRainDetails(
            HourlyExceedModels: hourlyHits.Count,
            DailyExceedModels: dailyExceeds ? 1 : 0);

        return new Alert(Type, severity, confidence, details,
            TriggerValue: hourlyExceeds ? Math.Round(maxHourly, 1) : Math.Round(dailySum, 1),
            Threshold: hourlyExceeds ? hourlyThreshold : dailyThreshold);
    }
}
