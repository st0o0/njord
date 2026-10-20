using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Flood;
using Njord.Messages.Alerts;

namespace Njord.Flood;

public sealed class FloodRiskAlertRule : IAlertRule<DomainForecast<FloodPoint>>
{
    public const int ForecastDays = 7;
    public const double MedianFactor = 2.0;
    public const double HighConfidenceShare = 0.7;
    public const double LowConfidenceShare = 0.3;

    public AlertType Type => AlertType.FloodRisk;

    public FeatureDescriptor Feature { get; } = new(
        FeatureKind.FloodRisk,
        FeatureCategory.Flood,
        "River discharge flood risk warning",
        DefaultEnabled: false,
        new DomainRequirements(Flood: FloodParams.Discharge | FloodParams.DischargeMedian | FloodParams.DischargeP75
            | FloodParams.DischargeMax));

    public Alert Evaluate(DomainForecast<FloodPoint> forecast, AlertOptions options, DateTimeOffset now)
    {
        var best = Alert.None(Type);
        foreach (var point in forecast.NextDays(now, ForecastDays))
        {
            if (point.Discharge is not { } discharge)
            {
                continue;
            }

            var (severity, threshold) = Classify(point);
            var (share, level) = Confidence(point, threshold);
            if (severity > best.Severity
                || (severity == best.Severity && severity != AlertSeverity.None && discharge > best.TriggerValue))
            {
                best = Build(Type, severity, Math.Round(discharge, 1), Math.Round(threshold, 1), point.HoursFrom(now))
                    with
                { Confidence = share, Level = level };
            }
        }

        return best;
    }

    internal static Alert Build(AlertType type, AlertSeverity severity, double trigger, double threshold, int hoursUntil) =>
        new(type, severity, 1.0, NoDetails.Instance, TriggerValue: trigger, Threshold: threshold, HoursUntil: hoursUntil, Level: ConfidenceLevel.High);

    internal static (double Share, ConfidenceLevel Level) Confidence(FloodPoint point, double threshold)
    {
        if (point.EnsembleMembers is not { Count: > 0 } members)
        {
            return (1.0, ConfidenceLevel.High);
        }

        var share = (double)members.Count(m => m > threshold) / members.Count;
        var level = share switch
        {
            >= HighConfidenceShare => ConfidenceLevel.High,
            < LowConfidenceShare => ConfidenceLevel.Low,
            _ => ConfidenceLevel.Medium,
        };
        return (share, level);
    }

    public static (AlertSeverity Severity, double Threshold) Classify(FloodPoint point)
    {
        if (point.DischargeMax is { } max && point.Discharge > max)
        {
            return (AlertSeverity.Red, max);
        }

        if (point.DischargeP75 is { } p75 && point.Discharge > p75)
        {
            return (AlertSeverity.Orange, p75);
        }

        if (point.DischargeMedian is { } median and > 0 && point.Discharge > median * MedianFactor)
        {
            return (AlertSeverity.Yellow, median * MedianFactor);
        }

        return (AlertSeverity.None, 0.0);
    }
}
