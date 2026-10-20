using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Marine;
using Njord.Messages.Alerts;

namespace Njord.Marine;

public sealed class SwimmingConditionsRule : IAlertRule<DomainForecast<MarinePoint>>
{
    public const int WindowHours = 24;
    public const double ModerateWaveHeightMeters = 1.0;
    public const double DangerousWaveHeightMeters = 2.0;
    public const double ModerateWaterTemperatureCelsius = 16.0;
    public const double DangerousWaterTemperatureCelsius = 10.0;

    public AlertType Type => AlertType.SwimmingConditions;

    public Alert Evaluate(DomainForecast<MarinePoint> forecast, AlertOptions options, DateTimeOffset now)
    {
        var start = AlertClock.HourStart(now);
        var best = Alert.None(Type);

        foreach (var point in ForecastQueries.Window(forecast, start, start.AddHours(WindowHours)))
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
                    Type, severity, 1.0, NoDetails.Instance,
                    TriggerValue: Math.Round(height, 1),
                    Threshold: threshold,
                    HoursUntil: Math.Max(0, (int)(point.ValidFrom - start).TotalHours),
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

        if (point.WaterTemperature is { } cold && cold < DangerousWaterTemperatureCelsius)
        {
            return (AlertSeverity.Red, DangerousWaterTemperatureCelsius);
        }

        if (point.WaveHeight >= ModerateWaveHeightMeters)
        {
            return (AlertSeverity.Yellow, ModerateWaveHeightMeters);
        }

        if (point.WaterTemperature is { } cool && cool < ModerateWaterTemperatureCelsius)
        {
            return (AlertSeverity.Yellow, ModerateWaterTemperatureCelsius);
        }

        return (AlertSeverity.None, 0.0);
    }
}
