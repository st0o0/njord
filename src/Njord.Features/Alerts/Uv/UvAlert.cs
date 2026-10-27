using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Alerts.Shared;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Features.Alerts.Uv;

public sealed class UvAlert(IOptions<UvConfig> config) : FeatureRule<WeatherInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Weather,
        Name: "Uv",
        DefaultEnabled: true,
        new DomainRequirements(Weather: WeatherParams.UvIndex));

    public override bool Enabled => config.Value.Enabled;

    protected override WeatherInput? Project(LocationForecast forecast) =>
        forecast.Weather is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<WeatherInput> ctx)
    {
        if (ctx.Current.Weather.NextHoursInclusive(ctx.Now).HighestBy(p => p.UvIndex) is not { UvIndex: { } uvIndex } peak || uvIndex <= 0)
        {
            return null;
        }

        var (level, severity, uvThreshold) = uvIndex switch
        {
            >= 11 => (UvLevel.Extreme, AlertSeverity.Red, 11.0),
            >= 8 => (UvLevel.VeryHigh, AlertSeverity.Red, 8.0),
            >= 6 => (UvLevel.High, AlertSeverity.Orange, 6.0),
            >= 3 => (UvLevel.Moderate, AlertSeverity.Yellow, 3.0),
            _ => (UvLevel.Moderate, AlertSeverity.None, 0.0),
        };

        if (severity == AlertSeverity.None)
        {
            return null;
        }

        var rounded = Math.Round(uvIndex, 1);
        var confidence = ConfidenceLevel.Medium;
        return new UvAlertResult(
            ctx.Location, severity, confidence.ToConfidence(), rounded, uvThreshold,
            PeakValue: null, HoursUntil: peak.HoursFrom(ctx.Now), DurationHours: null, Level: confidence,
            UvLevel: level, UvIndex: rounded);
    }
}
