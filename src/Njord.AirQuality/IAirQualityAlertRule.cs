using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.AirQuality;

public interface IAirQualityAlertRule
{
    AlertType Type { get; }

    Alert Evaluate(DomainForecast<AirQualityPoint> forecast, AlertSettings settings, DateTimeOffset now);
}
