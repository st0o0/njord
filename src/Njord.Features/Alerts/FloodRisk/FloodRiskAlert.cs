using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Flood;
using Njord.Messages.Alerts;
using Njord.Messages.Results;
using Njord.Messages.Results.Alerts;
using Njord.Messages.Stream;

namespace Njord.Features.Alerts.FloodRisk;

public sealed class FloodRiskAlert(IOptions<FloodRiskConfig> config) : FeatureRule<FloodInput>
{
    public const int ForecastDays = 7;
    public const double MedianFactor = 2.0;
    public const double HighConfidenceShare = 0.7;
    public const double LowConfidenceShare = 0.3;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Flood,
        Name: "FloodRisk",
        new DomainRequirements(Flood: FloodParams.Discharge | FloodParams.DischargeMedian | FloodParams.DischargeP75
            | FloodParams.DischargeMax));

    public override bool Enabled => config.Value.Enabled;

    protected override FloodInput? Project(LocationForecast forecast) =>
        forecast is { Flood: { } f } ? new(f) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<FloodInput> ctx)
    {
        var best = (Severity: AlertSeverity.None, Trigger: 0.0, Threshold: 0.0, HoursUntil: 0, Confidence: 1.0, Level: ConfidenceLevel.High);
        foreach (var point in ctx.Current.Flood.NextDays(ctx.Now, ForecastDays))
        {
            if (point.Discharge is not { } discharge)
            {
                continue;
            }

            var (severity, threshold) = Classify(point);
            var (share, level) = Confidence(point, threshold);
            if (severity > best.Severity
                || (severity == best.Severity && severity != AlertSeverity.None && discharge > best.Trigger))
            {
                best = (severity, Math.Round(discharge, 1), Math.Round(threshold, 1), point.HoursFrom(ctx.Now), share, level);
            }
        }

        if (best.Severity == AlertSeverity.None)
        {
            return null;
        }

        return new FloodRiskAlertResult(
            ctx.Location, best.Severity, best.Confidence, best.Trigger, best.Threshold,
            PeakValue: null, HoursUntil: best.HoursUntil, DurationHours: null, Level: best.Level);
    }

    private static (double Share, ConfidenceLevel Level) Confidence(FloodPoint point, double threshold)
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

    private static (AlertSeverity Severity, double Threshold) Classify(FloodPoint point)
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
