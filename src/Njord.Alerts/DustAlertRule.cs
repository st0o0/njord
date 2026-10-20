using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Alerts;

public sealed class DustAlertRule : IAlertRule<DomainForecast<AirQualityPoint>>
{
    public string Name => "Dust";

    public FeatureDescriptor Feature { get; } = new(
        OutputKind.Alert,
        FeatureCategory.AirQuality,
        DefaultEnabled: false,
        new DomainRequirements(AirQuality: AirQualityParams.Dust));

    public Alert Evaluate(DomainForecast<AirQualityPoint> forecast, AlertOptions options, DateTimeOffset now)
    {
        var thresholds = options.DustThresholds;
        AirQualityPoint? peakPoint = null;
        var peak = 0.0;
        foreach (var point in forecast.NextHoursInclusive(now))
        {
            if (point.Dust is { } dust && dust > peak)
            {
                peak = dust;
                peakPoint = point;
            }
        }

        if (peakPoint is null)
        {
            return Alert.None();
        }

        var severity = AirQualityAlertWindow.SeverityFor(peak, thresholds[0], thresholds[1], thresholds[2]);
        if (severity == AlertSeverity.None)
        {
            return Alert.None();
        }

        var threshold = severity switch
        {
            AlertSeverity.Red => thresholds[2],
            AlertSeverity.Orange => thresholds[1],
            _ => thresholds[0],
        };
        return new Alert(severity, 1.0, NoDetails.Instance, TriggerValue: Math.Round(peak, 1), Threshold: threshold, HoursUntil: peakPoint.HoursFrom(now), Level: ConfidenceLevel.High);
    }
}
