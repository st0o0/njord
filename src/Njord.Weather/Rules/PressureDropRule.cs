using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;

namespace Njord.Weather.Alerts.Rules;

public sealed class PressureDropRule : IAlertRule<DomainForecast<WeatherPoint>>
{
    private const int DropSpanHours = 3;
    private const int MinimumHours = 4;

    public AlertType Type => AlertType.PressureDrop;

    public Alert Evaluate(DomainForecast<WeatherPoint> forecast, AlertOptions options, DateTimeOffset now)
    {
        var window = forecast.Next24Hours(now);
        if (window.Count < MinimumHours)
        {
            return Alert.None(Type);
        }

        var (maxDrop, endPoint) = MaxDrop(window, now);
        var dropThreshold = options.PressureDropThreshold;
        var severeThreshold = options.PressureDropSevereThreshold;

        var severity = maxDrop switch
        {
            _ when maxDrop >= severeThreshold => AlertSeverity.Orange,
            _ when maxDrop >= dropThreshold => AlertSeverity.Yellow,
            _ => AlertSeverity.None,
        };

        var rounded = Math.Round(maxDrop, 1);
        return AlertWindow.Build(Type, severity, endPoint, new PressureDropDetails(MaxDrop: rounded, ModelsAgreeing: 0),
            rounded, maxDrop >= severeThreshold ? severeThreshold : dropThreshold);
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
