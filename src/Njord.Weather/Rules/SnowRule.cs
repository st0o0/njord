using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.Weather.Alerts.Rules;

public sealed class SnowRule : IAlertRule<DomainForecast<WeatherPoint>>
{
    public AlertType Type => AlertType.Snow;

    public Alert Evaluate(DomainForecast<WeatherPoint> forecast, AlertOptions options, DateTimeOffset now)
    {
        var window = forecast.Next24Hours(now);
        if (!window.Any(p => p.Snowfall is not null))
        {
            return Alert.None(Type);
        }

        var totalSum = window.Sum(p => p.Snowfall ?? 0.0);
        var snowiest = window.HighestBy(p => p.Snowfall);
        var freezingLevels = window.Select(p => p.FreezingLevelHeight).OfType<double>().Order().ToList();

        var (severity, snowThreshold) = totalSum switch
        {
            > 20 => (AlertSeverity.Red, 20.0),
            > 5 => (AlertSeverity.Orange, 5.0),
            > 0 => (AlertSeverity.Yellow, 0.0),
            _ => (AlertSeverity.None, 0.0),
        };

        var roundedSum = Math.Round(totalSum, 1);
        var details = new SnowDetails(
            ExpectedAccumulation: roundedSum,
            FreezingLevel: freezingLevels.Count > 0 ? Math.Round(Median(freezingLevels), 0) : null,
            ModelsAgreeing: 0);

        return AlertWindow.Build(Type, severity, snowiest, details, roundedSum, snowThreshold);
    }

    private static double Median(List<double> sorted)
    {
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }
}
