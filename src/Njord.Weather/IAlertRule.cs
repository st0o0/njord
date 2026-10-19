using Njord.Alerts;
using Njord.Model;

namespace Njord.Weather.Alerts;

public interface IAlertRule : IAlertRule<DomainForecast<WeatherPoint>>
{
}
