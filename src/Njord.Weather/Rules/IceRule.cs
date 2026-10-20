using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;

namespace Njord.Weather.Alerts.Rules;

public sealed class IceRule : IAlertRule<DomainForecast<WeatherPoint>>
{
    public AlertType Type => AlertType.Ice;

    public Alert Evaluate(DomainForecast<WeatherPoint> forecast, AlertOptions options, DateTimeOffset now)
    {
        var iceThreshold = options.IceThreshold;
        var icy = forecast.NextHoursInclusive(now)
            .Where(p => (p.Rain ?? p.Precipitation) > 0 && p.Temperature <= iceThreshold)
            .ToList();
        if (icy.MinBy(p => p.Temperature) is not { Temperature: { } minTemp } coldest)
        {
            return Alert.None(Type);
        }

        var soilFrozen = icy.Any(p => p.SoilTemperature is <= 0);
        var severity = (minTemp, soilFrozen) switch
        {
            ( <= 0, true) => AlertSeverity.Red,
            ( <= 0, false) => AlertSeverity.Orange,
            _ => AlertSeverity.Yellow,
        };

        var rounded = Math.Round(minTemp, 1);
        var details = new IceDetails(ExpectedLow: rounded, RainHours: icy.Count, SoilFrozen: soilFrozen);
        return AlertWindow.Build(Type, severity, coldest, details, rounded, iceThreshold,
            hoursUntil: icy[0].HoursFrom(now));
    }
}
