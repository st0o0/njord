using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.Weather.Alerts;

public interface IAlertRule
{
    AlertType Type { get; }

    Alert Evaluate(DomainForecast<WeatherPoint> forecast, AlertSettings settings, DateTimeOffset now);
}
