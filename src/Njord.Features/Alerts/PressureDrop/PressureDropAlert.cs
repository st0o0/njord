using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Alerts;
using Njord.Messages.Results;
using Njord.Messages.Results.Alerts;
using Njord.Messages.Stream;

namespace Njord.Features.Alerts.PressureDrop;

public sealed class PressureDropAlert(IOptions<PressureDropConfig> config) : FeatureRule<ForecastInput>
{
    private const int DropSpanHours = 3;
    private const int MinimumHours = 4;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Forecast,
        Name: "PressureDrop",
        new DomainRequirements(Forecast: ForecastParams.Pressure),
        EntityMetadata: new EntityMetadata(EntityPlatform.BinarySensor, DeviceClass: "safety"));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } w ? new ForecastInput(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx)
    {
        var window = ctx.Current.Forecast.NextHoursInclusive(ctx.Now, config.Value.WindowHours).ToList();
        if (window.Count < MinimumHours)
        {
            return null;
        }

        var maxDrop = MaxDrop(window, ctx.Now);
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
        const ConfidenceLevel level = ConfidenceLevel.Medium;
        return new PressureDropAlertResult(
            ctx.Location, severity, level.ToConfidence(), rounded, maxDrop >= severeThreshold ? severeThreshold : dropThreshold,
            PeakValue: null, HoursUntil: null, DurationHours: null, Level: level,
            MaxDrop: rounded);
    }

    private static double MaxDrop(List<ForecastPoint> window, DateTimeOffset now)
    {
        var maxDrop = 0.0;
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
                }
            }
        }

        return maxDrop;
    }
}
