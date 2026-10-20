using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Alerts.Shared;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Features.Alerts.Thunderstorm;

public sealed class ThunderstormAlert(IOptions<ThunderstormConfig> config) : FeatureRule<WeatherInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Weather,
        DefaultEnabled: true,
        new DomainRequirements(Weather: WeatherParams.Cape | WeatherParams.Precipitation | WeatherParams.WindGusts | WeatherParams.EnsembleStdDev));

    public override bool Enabled => config.Value.Enabled;

    protected override WeatherInput? Project(LocationForecast forecast) =>
        forecast.Weather is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<WeatherInput> ctx)
    {
        var evaluable = ctx.Current.Weather.NextHoursInclusive(ctx.Now).Where(p => p.Cape is not null && p.WindGusts is not null).ToList();
        if (evaluable.Count == 0)
        {
            return null;
        }

        var meetAll = evaluable
            .Where(p => p.Cape > config.Value.CapeThreshold
                && p.Precipitation > config.Value.PrecipThreshold
                && p.WindGusts > config.Value.GustThreshold)
            .ToList();
        var share = (double)meetAll.Count / evaluable.Count;
        var severity = share switch
        {
            > 0.75 => AlertSeverity.Red,
            >= 0.5 => AlertSeverity.Orange,
            > 0 => AlertSeverity.Yellow,
            _ => AlertSeverity.None,
        };

        if (severity == AlertSeverity.None)
        {
            return null;
        }

        var peak = (meetAll.Count > 0 ? meetAll : evaluable).HighestBy(p => p.Cape);
        var maxCape = evaluable.Max(p => p.Cape) ?? 0.0;
        var level = ConfidenceThresholds.Default.LevelFor(peak?.EnsembleStdDev);
        return new ThunderstormAlertResult(
            ctx.Location, severity, level.ToConfidence(), Math.Round(maxCape, 0), config.Value.CapeThreshold,
            PeakValue: null, HoursUntil: meetAll.Count > 0 ? meetAll[0].HoursFrom(ctx.Now) : null, DurationHours: null, Level: level);
    }
}
