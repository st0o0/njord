using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Forecast.Marine;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Features.Alerts.SwimmingConditions;

public sealed class SwimmingConditionsAlert(IOptions<SwimmingConditionsConfig> config) : FeatureRule<MarineWeatherInput>
{
    public const int WindowHours = 24;
    public const double ModerateWaveHeightMeters = 1.0;
    public const double DangerousWaveHeightMeters = 2.0;
    public const double ModerateWaterTemperatureCelsius = 16.0;
    public const double DangerousWaterTemperatureCelsius = 10.0;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Marine,
        Name: "Swimming",
        DefaultEnabled: false,
        new DomainRequirements(Marine: MarineParams.WaveHeight,
            OptionalMarine: MarineParams.WaterTemperature));

    public override bool Enabled => config.Value.Enabled;

    protected override MarineWeatherInput? Project(LocationForecast forecast) =>
        forecast is { Marine: { } m, Weather: { } w } ? new(m, w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<MarineWeatherInput> ctx)
    {
        var best = (Severity: AlertSeverity.None, Trigger: 0.0, Threshold: 0.0, HoursUntil: 0);

        foreach (var point in ctx.Current.Marine.NextHoursInclusive(ctx.Now, WindowHours))
        {
            if (point.WaveHeight is not { } height)
            {
                continue;
            }

            var (severity, threshold) = Classify(point);
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

    public static (AlertSeverity Severity, double Threshold) Classify(MarinePoint point)
    {
        if (point.WaveHeight >= DangerousWaveHeightMeters)
        {
            return (AlertSeverity.Red, DangerousWaveHeightMeters);
        }

        if (point.WaterTemperature is < DangerousWaterTemperatureCelsius)
        {
            return (AlertSeverity.Red, DangerousWaterTemperatureCelsius);
        }

        if (point.WaveHeight >= ModerateWaveHeightMeters)
        {
            return (AlertSeverity.Yellow, ModerateWaveHeightMeters);
        }

        if (point.WaterTemperature is < ModerateWaterTemperatureCelsius)
        {
            return (AlertSeverity.Yellow, ModerateWaterTemperatureCelsius);
        }

        return (AlertSeverity.None, 0.0);
    }
}
