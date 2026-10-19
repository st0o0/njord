using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.Alerts;

public interface IAlertRule<in TInput>
{
    AlertType Type { get; }

    Alert Evaluate(TInput input, AlertSettings settings, DateTimeOffset now);
}

public interface IAlertRule : IAlertRule<DomainForecast<WeatherPoint>>
{
}
