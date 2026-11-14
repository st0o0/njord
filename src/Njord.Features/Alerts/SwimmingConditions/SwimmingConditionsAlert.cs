using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Marine;
using Njord.Messages.Alerts;
using Njord.Messages.Results;
using Njord.Messages.Results.Alerts;
using Njord.Messages.Stream;

namespace Njord.Features.Alerts.SwimmingConditions;

public sealed class SwimmingConditionsAlert(IOptions<SwimmingConditionsConfig> config) : FeatureRule<MarineInput>
{

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Marine,
        Name: "Swimming",
        new DomainRequirements(Marine: MarineParams.WaveHeight,
            OptionalMarine: MarineParams.WaterTemperature),
        EntityMetadata: new EntityMetadata(EntityPlatform.BinarySensor, DeviceClass: "safety"));

    public override bool Enabled => config.Value.Enabled;

    protected override MarineInput? Project(LocationForecast forecast) =>
        forecast is { Marine: { } m } ? new MarineInput(m) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<MarineInput> ctx)
    {
        var cfg = config.Value;
        var best = (Severity: AlertSeverity.None, Trigger: 0.0, Threshold: 0.0, HoursUntil: 0);

        foreach (var point in ctx.Current.Marine.NextHoursInclusive(ctx.Now, cfg.WindowHours))
        {
            if (point.WaveHeight is not { } height)
            {
                continue;
            }

            var (severity, threshold) = Classify(point, cfg);
            if (severity > best.Severity
                || (severity == best.Severity && severity != AlertSeverity.None && height > best.Trigger))
            {
                best = (severity, Math.Round(height, 1), threshold, point.HoursFrom(ctx.Now));
            }
        }

        if (best.Severity == AlertSeverity.None)
        {
            return null;
        }

        return new SwimmingConditionsAlertResult(
            ctx.Location, best.Severity, 1.0, best.Trigger, best.Threshold,
            PeakValue: null, HoursUntil: best.HoursUntil, DurationHours: null, Level: ConfidenceLevel.High);
    }

    public static (AlertSeverity Severity, double Threshold) Classify(MarinePoint point, SwimmingConditionsConfig cfg)
    {
        if (point.WaveHeight >= cfg.DangerousWaveHeightM)
        {
            return (AlertSeverity.Red, cfg.DangerousWaveHeightM);
        }

        if (point.WaterTemperature < cfg.DangerousWaterTempCelsius)
        {
            return (AlertSeverity.Red, cfg.DangerousWaterTempCelsius);
        }

        if (point.WaveHeight >= cfg.ModerateWaveHeightM)
        {
            return (AlertSeverity.Yellow, cfg.ModerateWaveHeightM);
        }

        if (point.WaterTemperature < cfg.ModerateWaterTempCelsius)
        {
            return (AlertSeverity.Yellow, cfg.ModerateWaterTempCelsius);
        }

        return (AlertSeverity.None, 0.0);
    }
}
