using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Alerts.Shared;
using Njord.Messages.Alerts;
using Njord.Messages.Results;
using Njord.Messages.Results.Alerts;
using Njord.Messages.Stream;

namespace Njord.Features.Alerts.Humidity;

public sealed class HumidityAlert(IOptions<HumidityConfig> config) : FeatureRule<ForecastInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Forecast,
        Name: "Humidity",
        new DomainRequirements(Forecast: ForecastParams.DewPoint | ForecastParams.IsDay));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx)
    {
        var thresholds = config.Value.Thresholds.ToSeverity(ScanDirection.Max);
        var day = ctx.Current.Forecast.NextHoursInclusive(ctx.Now).Where(p => p is { IsDay: > 0.5, DewPoint: not null }).ToList();
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
        var level = ConfidenceLevel.Medium;
        return new HumidityAlertResult(
            ctx.Location, severity, level.ToConfidence(), rounded, effectiveThreshold,
            PeakValue: null, HoursUntil: muggiest.HoursFrom(ctx.Now), DurationHours: null, Level: level,
            MaxDewpoint: rounded, ComfortLevel: comfortLevel);
    }
}
