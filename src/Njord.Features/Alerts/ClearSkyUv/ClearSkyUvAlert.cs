using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Alerts.Shared;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Features.Alerts.ClearSkyUv;

public sealed class ClearSkyUvAlert(IOptions<ClearSkyUvConfig> config) : FeatureRule<WeatherInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Lifestyle,
        Name: "ClearSkyUv",
        DefaultEnabled: false,
        new DomainRequirements(Weather: WeatherParams.UvIndexClearSky | WeatherParams.IsDay));

    public override bool Enabled => config.Value.Enabled;

    protected override WeatherInput? Project(LocationForecast forecast) =>
        forecast.Weather is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<WeatherInput> ctx)
    {
        var thresholds = config.Value.EffectiveThresholds.ToSeverity(ScanDirection.Max);
        var daytime = ctx.Current.Weather.NextHoursInclusive(ctx.Now)
            .Where(p => p.IsDay is > 0)
            .ToList();
        if (daytime.HighestBy(p => p.UvIndexClearSky) is not { UvIndexClearSky: { } peak } highest)
        {
            return null;
        }

        var (severity, effectiveThreshold) = thresholds.Classify(peak);
        if (severity == AlertSeverity.None)
        {
            return null;
        }

        var rounded = Math.Round(peak, 1);
        var level = ConfidenceLevel.Medium;
        return new ClearSkyUvAlertResult(
            ctx.Location, severity, level.ToConfidence(), rounded, effectiveThreshold,
            PeakValue: null, HoursUntil: highest.HoursFrom(ctx.Now), DurationHours: null, Level: level,
            PeakClearSkyUv: rounded);
    }
}
