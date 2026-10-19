using Njord.Messages.Alerts;
using Njord.Mqtt.Payloads;

namespace Njord.Mqtt.Mapping;

internal static class AnalysisPayloadMappings
{
    public static AlertPayload ToPayload(this AirQualityAlert alert)
        => new NoDetailsAlertPayload().WithThreshold(alert.Severity, alert.TriggerValue, alert.Threshold, alert.HoursUntil);

    public static AlertPayload ToPayload(this FloodAlert alert)
        => new NoDetailsAlertPayload().WithThreshold(alert.Severity, alert.TriggerValue, alert.Threshold, alert.HoursUntil);

    public static AlertPayload ToPayload(this Alert alert)
    {
        AlertPayload payload = alert.Details switch
        {
            FrostDetails d => new FrostAlertPayload(
                d.ExpectedLow,
                d.EarliestFrost is { } earliest ? $"+{earliest.Offset}h" : null,
                d.ModelsAgreeing),
            HeatDetails d => new HeatAlertPayload(d.ExpectedMax, d.ModelsAgreeing),
            StormDetails d => new StormAlertPayload(d.ExpectedMaxGust, d.ModelsAgreeing),
            HeavyRainDetails d => new HeavyRainAlertPayload(d.HourlyExceedModels, d.DailyExceedModels),
            UvDetails d => new UvAlertPayload(d.Level, d.UvIndex),
            FogDetails d => new FogAlertPayload(d.FogHours, d.ModelsAgreeing),
            SnowDetails d => new SnowAlertPayload(d.ExpectedAccumulation, d.FreezingLevel, d.ModelsAgreeing),
            PressureDropDetails d => new PressureDropAlertPayload(d.MaxDrop, d.ModelsAgreeing),
            ThunderstormDetails d => new ThunderstormAlertPayload(d.ModelsAgreeing),
            IceDetails d => new IceAlertPayload(d.ExpectedLow, d.RainHours, d.SoilFrozen),
            WindChillDetails d => new WindChillAlertPayload(d.FeltTemperature, d.WindFactor, d.ExposureRisk),
            VisibilityDetails d => new VisibilityAlertPayload(d.MinVisibility, d.HoursBelow1000m),
            TropicalNightDetails d => new TropicalNightAlertPayload(d.NightMinimum, d.HoursAbove20),
            HumidityDetails d => new HumidityAlertPayload(d.MaxDewpoint, d.ComfortLevel),
            NoDetails => new NoDetailsAlertPayload(),
            _ => throw new ArgumentOutOfRangeException(nameof(alert), alert.Details, null),
        };

        return payload with
        {
            Severity = alert.Severity,
            Confidence = alert.Confidence,
            TriggerValue = alert.TriggerValue,
            Threshold = alert.Threshold,
            PeakValue = alert.PeakValue,
            HoursUntil = alert.HoursUntil,
            DurationHours = alert.DurationHours,
        };
    }

    private static AlertPayload WithThreshold(
        this AlertPayload payload, AlertSeverity severity, double triggerValue, double threshold, int? hoursUntil)
        => payload with
        {
            Severity = severity,
            Confidence = 1.0,
            TriggerValue = triggerValue,
            Threshold = threshold,
            HoursUntil = hoursUntil,
        };
}
