using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.Alerts.Rules;

public sealed class ThunderstormRule : IAlertRule
{
    public AlertType Type => AlertType.Thunderstorm;

    public Alert Evaluate(DomainForecast<WeatherPoint> forecast, AlertSettings settings, DateTimeOffset now)
    {
        var evaluable = forecast.Next24Hours(now).Where(p => p.Cape is not null && p.WindGusts is not null).ToList();
        if (evaluable.Count == 0)
        {
            return Alert.None(Type);
        }

        var meetAll = evaluable
            .Where(p => p.Cape > settings.CapeThreshold
                && p.Precipitation > settings.ThunderstormPrecipThreshold
                && p.WindGusts > settings.ThunderstormGustThreshold)
            .ToList();
        var share = (double)meetAll.Count / evaluable.Count;
        var severity = share switch
        {
            > 0.75 => AlertSeverity.Red,
            >= 0.5 => AlertSeverity.Orange,
            > 0 => AlertSeverity.Yellow,
            _ => AlertSeverity.None,
        };

        var peak = (meetAll.Count > 0 ? meetAll : evaluable).HighestBy(p => p.Cape);
        var maxCape = evaluable.Max(p => p.Cape) ?? 0.0;
        return settings.Build(Type, severity, peak, new ThunderstormDetails(ModelsAgreeing: 0),
            Math.Round(maxCape, 0), settings.CapeThreshold,
            hoursUntil: meetAll.Count > 0 ? meetAll[0].HoursFrom(now) : null);
    }
}
