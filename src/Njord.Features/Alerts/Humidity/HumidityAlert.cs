using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Features.Alerts.Humidity;

public sealed class HumidityAlert(IOptions<HumidityConfig> config) : FeatureRule<WeatherInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Weather,
        DefaultEnabled: true,
        new DomainRequirements(Weather: WeatherParams.DewPoint | WeatherParams.IsDay | WeatherParams.EnsembleStdDev));

    public override bool Enabled => config.Value.Enabled;

    protected override WeatherInput? Project(LocationForecast forecast) =>
        forecast.Weather is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<WeatherInput> ctx)
    {
        var thresholds = SeverityThresholds.TryCreate(config.Value.Thresholds, ScanDirection.Max, out var t)
            ? t
            : throw new InvalidOperationException("Invalid humidity thresholds");
        var day = ctx.Current.Weather.NextHoursInclusive(ctx.Now).Where(p => p is { IsDay: > 0.5, DewPoint: not null }).ToList();
        if (day.MaxBy(p => p.DewPoint) is not { DewPoint: { } dewPoint } muggiest)
        {
            return null;
        }

        var (severity, effectiveThreshold) = thresholds.Classify(dewPoint);
        if (severity == AlertSeverity.None)
        {
            return null;
        }

        var comfortLevel = severity switch
        {
            AlertSeverity.Red => ComfortLevel.Tropical,
            AlertSeverity.Orange => ComfortLevel.Oppressive,
            AlertSeverity.Yellow => ComfortLevel.Muggy,
            _ => (ComfortLevel?)null,
        };

        var rounded = Math.Round(dewPoint, 1);
        var level = ConfidenceThresholds.Default.LevelFor(muggiest.EnsembleStdDev);
        return new HumidityAlertResult(
            ctx.Location, severity, level.ToConfidence(), rounded, effectiveThreshold,
            PeakValue: null, HoursUntil: muggiest.HoursFrom(ctx.Now), DurationHours: null, Level: level,
            MaxDewpoint: rounded, ComfortLevel: comfortLevel);
    }
}
