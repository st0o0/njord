using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Alerts;
using Njord.Messages.Results;
using Njord.Messages.Results.Alerts;
using Njord.Messages.Stream;

namespace Njord.Features.Alerts.Fog;

public sealed class FogAlert(IOptions<FogConfig> config) : FeatureRule<ForecastInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Forecast,
        Name: "Fog",
        new DomainRequirements(Forecast: ForecastParams.Temperature | ForecastParams.DewPoint | ForecastParams.WindSpeed | ForecastParams.Humidity));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx)
    {
        var fogPoints = ctx.Current.Forecast.NextHoursInclusive(ctx.Now)
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
        var level = ConfidenceLevel.Medium;
        return new FogAlertResult(
            ctx.Location, severity, level.ToConfidence(), fogHours, 1,
            PeakValue: null, HoursUntil: first.HoursFrom(ctx.Now), DurationHours: fogHours, Level: level,
            FogHours: fogHours);
    }
}
