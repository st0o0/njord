using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Forecast.AirQuality;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Features.Alerts.Pollen;

public sealed class PollenAlert(IOptions<PollenConfig> config) : FeatureRule<AqInput>
{
    public const double YellowThreshold = 20.0;
    public const double OrangeThreshold = 50.0;
    public const double RedThreshold = 100.0;

    private static readonly SeverityThresholds Thresholds = new(YellowThreshold, OrangeThreshold, RedThreshold, ScanDirection.Max);

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.AirQuality,
        Name: "Pollen",
        DefaultEnabled: true,
        new DomainRequirements(AirQuality: AirQualityParams.BirchPollen | AirQualityParams.GrassPollen
            | AirQualityParams.Alder | AirQualityParams.Mugwort));

    public override bool Enabled => config.Value.Enabled;

    protected override AqInput? Project(LocationForecast forecast) =>
        forecast.AirQuality is { } aq ? new(aq) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<AqInput> ctx)
    {
        AirQualityPoint? peakPoint = null;
        var peak = 0.0;
        foreach (var point in ctx.Current.AirQuality.NextHoursInclusive(ctx.Now))
        {
            var value = HighestPollen(point);
            if (value > peak)
            {
                peak = value;
                peakPoint = point;
            }
        }

        if (peakPoint is null)
        {
            return null;
        }

        var (severity, threshold) = Thresholds.Classify(peak);
        if (severity == AlertSeverity.None)
        {
            return null;
        }

        return new PollenAlertResult(
            ctx.Location, severity, 1.0, Math.Round(peak, 1), threshold,
            PeakValue: null, HoursUntil: peakPoint.HoursFrom(ctx.Now), DurationHours: null, Level: ConfidenceLevel.High);
    }

    private static double HighestPollen(AirQualityPoint point) =>
        Math.Max(
            Math.Max(point.BirchPollen ?? 0, point.GrassPollen ?? 0),
            Math.Max(point.Alder ?? 0, point.Mugwort ?? 0));
}
