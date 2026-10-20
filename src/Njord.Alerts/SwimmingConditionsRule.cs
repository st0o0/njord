using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Marine;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Alerts;

public sealed class SwimmingConditionsRule : IAlertRule<DomainForecast<MarinePoint>>
{
    public const int WindowHours = 24;
    public const double ModerateWaveHeightMeters = 1.0;
    public const double DangerousWaveHeightMeters = 2.0;
    public const double ModerateWaterTemperatureCelsius = 16.0;
    public const double DangerousWaterTemperatureCelsius = 10.0;


    public string Name => "Swimming";

    public FeatureDescriptor Feature { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Marine,
        DefaultEnabled: false,
        new DomainRequirements(Marine: MarineParams.WaveHeight,
            OptionalMarine: MarineParams.WaterTemperature));

    public Alert Evaluate(DomainForecast<MarinePoint> forecast, AlertOptions options, DateTimeOffset now)
    {
        var best = Alert.None();

        foreach (var point in forecast.NextHoursInclusive(now, WindowHours))
        {
            if (point.WaveHeight is not { } height)
            {
                continue;
            }

            var (severity, threshold) = Classify(point);
            if (severity > best.Severity
                || (severity == best.Severity && severity != AlertSeverity.None && height > best.TriggerValue))
            {
                best = new Alert(
                    severity, 1.0, NoDetails.Instance,
                    TriggerValue: Math.Round(height, 1),
                    Threshold: threshold,
                    HoursUntil: point.HoursFrom(now),
                    Level: ConfidenceLevel.High);
            }
        }

        return best;
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
