using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Features.Alerts.Fog;

public sealed class FogAlert(IOptions<FogConfig> config) : FeatureRule<WeatherInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Weather,
        Name: "Fog",
        DefaultEnabled: true,
        new DomainRequirements(Weather: WeatherParams.Temperature | WeatherParams.DewPoint | WeatherParams.WindSpeed | WeatherParams.Humidity | WeatherParams.EnsembleStdDev));

    public override bool Enabled => config.Value.Enabled;

    protected override WeatherInput? Project(LocationForecast forecast) =>
        forecast.Weather is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<WeatherInput> ctx)
    {
        var fogPoints = ctx.Current.Weather.NextHoursInclusive(ctx.Now)
            .Where(p => p.Temperature - p.DewPoint < 2.0 && p is { WindSpeed: < 3.0, Humidity: > 90.0 })
            .ToList();
        var fogHours = fogPoints.Count;
        var severity = fogHours switch
        {
            >= 1 when fogHours >= config.Value.PersistentHours => AlertSeverity.Orange,
            >= 1 => AlertSeverity.Yellow,
            _ => AlertSeverity.None,
        };

        if (severity == AlertSeverity.None)
        {
            return null;
        }

        var first = fogPoints[0];
        var level = ConfidenceThresholds.Default.LevelFor(first.EnsembleStdDev);
        return new FogAlertResult(
            ctx.Location, severity, level.ToConfidence(), fogHours, 1,
            PeakValue: null, HoursUntil: first.HoursFrom(ctx.Now), DurationHours: fogHours, Level: level,
            FogHours: fogHours);
    }
}
