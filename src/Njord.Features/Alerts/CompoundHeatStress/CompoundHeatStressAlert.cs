using Microsoft.Extensions.Options;
using Njord.Core.AirQuality;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Alerts;
using Njord.Messages.Results;
using Njord.Messages.Results.Alerts;
using Njord.Messages.Stream;

namespace Njord.Features.Alerts.CompoundHeatStress;

public sealed class CompoundHeatStressAlert(IOptions<CompoundHeatStressConfig> config) : FeatureRule<ForecastAqInput>
{
    private const double UvThreshold = 8.0;
    private const double UvPm25Threshold = 15.0;
    private const double VeryHighApparentMargin = 5.0;
    private const double VeryHighPm25Factor = 2.0;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Forecast,
        Name: "CompoundHeatStress",
        DefaultEnabled: false,
        new DomainRequirements(
            Forecast: ForecastParams.ApparentTemperature | ForecastParams.WetBulbTemperature | ForecastParams.UvIndex
                | ForecastParams.BoundaryLayerHeight | ForecastParams.Humidity,
            AirQuality: AirQualityParams.Pm25));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastAqInput? Project(LocationForecast forecast) =>
        forecast is { Forecast: { } w, AirQuality: { } aq } ? new(w, aq) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastAqInput> ctx)
    {
        var cfg = config.Value;
        var pm25ByTime = new Dictionary<DateTimeOffset, double>();
        foreach (var point in ctx.Current.AirQuality.NextHoursInclusive(ctx.Now))
        {
            if (point.Pm25 is { } pm)
            {
                pm25ByTime[point.ValidFrom] = pm;
            }
        }

        ForecastPoint? worstPoint = null;
        var worst = (Severity: AlertSeverity.None, Trigger: 0.0, Threshold: 0.0);
        foreach (var point in ctx.Current.Forecast.NextHoursInclusive(ctx.Now))
        {
            double? pm25 = pm25ByTime.TryGetValue(point.ValidFrom, out var pm) ? pm : null;
            var hour = Classify(cfg, point, pm25);
            if (hour.Severity > worst.Severity)
            {
                worst = hour;
                worstPoint = point;
            }
        }

        if (worstPoint is null)
        {
            return null;
        }

        return new CompoundHeatStressAlertResult(
            ctx.Location, worst.Severity, ConfidenceLevel.Medium.ToConfidence(),
            Math.Round(worst.Trigger, 1), worst.Threshold,
            PeakValue: null, HoursUntil: worstPoint.HoursFrom(ctx.Now), DurationHours: null, Level: ConfidenceLevel.Medium);
    }

    private static (AlertSeverity Severity, double Trigger, double Threshold) Classify(
        CompoundHeatStressConfig cfg, ForecastPoint point, double? pm25)
    {
        var best = (Severity: AlertSeverity.None, Trigger: 0.0, Threshold: 0.0);

        if (point.WetBulbTemperature is { } wetBulb)
        {
            if (wetBulb >= cfg.WetBulbExtreme)
            {
                best = Higher(best, AlertSeverity.Red, wetBulb, cfg.WetBulbExtreme);
            }
            else if (wetBulb >= cfg.WetBulbSevere)
            {
                best = Higher(best, AlertSeverity.Orange, wetBulb, cfg.WetBulbSevere);
            }
            else if (wetBulb >= cfg.WetBulbWarning)
            {
                best = Higher(best, AlertSeverity.Yellow, wetBulb, cfg.WetBulbWarning);
            }
        }

        if (point.ApparentTemperature is { } apparent && pm25 is { } fineParticles
            && apparent >= cfg.ApparentTempThreshold && fineParticles >= cfg.Pm25Threshold)
        {
            var veryHigh = apparent >= cfg.ApparentTempThreshold + VeryHighApparentMargin
                && fineParticles >= cfg.Pm25Threshold * VeryHighPm25Factor;
            best = Higher(best, veryHigh ? AlertSeverity.Orange : AlertSeverity.Yellow, apparent, cfg.ApparentTempThreshold);
        }

        if (point is { UvIndex: { } uv, BoundaryLayerHeight: { } boundary } && pm25 is { } particles
            && uv >= UvThreshold && boundary < cfg.BoundaryLayerLow && particles >= UvPm25Threshold)
        {
            best = Higher(best, AlertSeverity.Yellow, uv, UvThreshold);
        }

        return best;
    }

    private static (AlertSeverity Severity, double Trigger, double Threshold) Higher(
        (AlertSeverity Severity, double Trigger, double Threshold) current,
        AlertSeverity severity, double trigger, double threshold) =>
        severity > current.Severity ? (severity, trigger, threshold) : current;
}
