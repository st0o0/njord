using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Features.Alerts.PressureDrop;

public sealed class PressureDropAlert(IOptions<PressureDropConfig> config) : FeatureRule<WeatherInput>
{
    private const int DropSpanHours = 3;
    private const int MinimumHours = 4;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Weather,
        Name: "PressureDrop",
        DefaultEnabled: true,
        new DomainRequirements(Weather: WeatherParams.Pressure | WeatherParams.EnsembleStdDev));

    public override bool Enabled => config.Value.Enabled;

    protected override WeatherInput? Project(LocationForecast forecast) =>
        forecast.Weather is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<WeatherInput> ctx)
    {
        var window = ctx.Current.Weather.NextHoursInclusive(ctx.Now);
        if (window.Count < MinimumHours)
        {
            return null;
        }

        var (maxDrop, endPoint) = MaxDrop(window, ctx.Now);
        var dropThreshold = config.Value.Threshold;
        var severeThreshold = config.Value.SevereThreshold;

        var severity = maxDrop switch
        {
            _ when maxDrop >= severeThreshold => AlertSeverity.Orange,
            _ when maxDrop >= dropThreshold => AlertSeverity.Yellow,
            _ => AlertSeverity.None,
        };

        if (severity == AlertSeverity.None)
        {
            return null;
        }

        var rounded = Math.Round(maxDrop, 1);
        var level = ConfidenceThresholds.Default.LevelFor(endPoint?.EnsembleStdDev);
        return new PressureDropAlertResult(
            ctx.Location, severity, level.ToConfidence(), rounded, maxDrop >= severeThreshold ? severeThreshold : dropThreshold,
            PeakValue: null, HoursUntil: null, DurationHours: null, Level: level,
            MaxDrop: rounded);
    }

    private static (double Drop, WeatherPoint? End) MaxDrop(IReadOnlyList<WeatherPoint> window, DateTimeOffset now)
    {
        var maxDrop = 0.0;
        WeatherPoint? end = null;
        for (var i = 0; i < window.Count; i++)
        {
            for (var j = 0; j < i; j++)
            {
                if (window[i].HoursFrom(now) - window[j].HoursFrom(now) < DropSpanHours)
                {
                    continue;
                }

                if (window[j].Pressure is not { } before || window[i].Pressure is not { } after)
                {
                    continue;
                }

                var drop = before - after;
                if (drop > maxDrop)
                {
                    maxDrop = drop;
                    end = window[i];
                }
            }
        }

        return (maxDrop, end);
    }
}
