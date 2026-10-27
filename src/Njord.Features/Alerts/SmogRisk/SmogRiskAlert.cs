using Microsoft.Extensions.Options;
using Njord.Core.AirQuality;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Features.Alerts.SmogRisk;

public sealed class SmogRiskAlert(IOptions<SmogRiskConfig> config) : FeatureRule<ForecastAqInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.AirQuality,
        Name: "SmogRisk",
        DefaultEnabled: false,
        new DomainRequirements(Forecast: ForecastParams.BoundaryLayerHeight, AirQuality: AirQualityParams.Pm25));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastAqInput? Project(LocationForecast forecast) =>
        forecast is { Forecast: { } w, AirQuality: { } aq } ? new(w, aq) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastAqInput> ctx)
    {
        var cfg = config.Value;
        var weatherWindow = ctx.Current.Forecast.NextHoursInclusive(ctx.Now, 12);
        var aqWindow = ctx.Current.AirQuality.NextHoursInclusive(ctx.Now, 12);

        double? lowestBoundary = null;
        double? highestPm25 = null;

        foreach (var point in weatherWindow)
        {
            if (point.BoundaryLayerHeight is { } blh && (lowestBoundary is null || blh < lowestBoundary))
            {
                lowestBoundary = blh;
            }
        }

        foreach (var point in aqWindow)
        {
            if (point.Pm25 is { } pm && (highestPm25 is null || pm > highestPm25))
            {
                highestPm25 = pm;
            }
        }

        if (lowestBoundary is null || highestPm25 is null)
        {
            return null;
        }

        if (lowestBoundary >= cfg.BoundaryLayerThresholdM || highestPm25 < cfg.Pm25ThresholdUgm3)
        {
            return null;
        }

        var severity = (lowestBoundary, highestPm25) switch
        {
            ( < 200, >= 50) => AlertSeverity.Red,
            ( < 300, >= 35) => AlertSeverity.Orange,
            _ => AlertSeverity.Yellow,
        };

        return new SmogRiskAlertResult(
            ctx.Location, severity, ConfidenceLevel.Medium.ToConfidence(),
            Math.Round(highestPm25.Value, 1), cfg.Pm25ThresholdUgm3,
            PeakValue: null, HoursUntil: null, DurationHours: null, Level: ConfidenceLevel.Medium,
            BoundaryLayerHeight: Math.Round(lowestBoundary.Value, 0),
            Pm25: Math.Round(highestPm25.Value, 1));
    }
}
