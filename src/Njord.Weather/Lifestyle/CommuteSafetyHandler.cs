using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;

namespace Njord.Weather.Lifestyle;

public sealed class CommuteSafetyHandler : IDomainHandler
{
    public const double IceTemperature = 1.0;
    public const double DewPointSpread = 2.0;
    public const double FogHumidity = 95.0;
    public const double HeavyRain = 5.0;
    public const double StrongWind = 60.0 / 3.6;

    public string ConfigKey => "CommuteSafety";

    public DomainRequirements Requirements { get; } = new(Weather: WeatherParams.Temperature | WeatherParams.DewPoint | WeatherParams.Humidity
            | WeatherParams.Precipitation | WeatherParams.WindSpeed);

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        if (current.Weather is not { } weather)
        {
            return [];
        }

        var hours = ForecastQueries.Upcoming(weather, time.GetUtcNow(), 2);
        if (hours.Count == 0)
        {
            return [];
        }

        var ice = hours.Any(p => p.Temperature < IceTemperature && p.Temperature - p.DewPoint < DewPointSpread);
        var fog = hours.Any(p => p.Humidity > FogHumidity && p.Temperature - p.DewPoint < DewPointSpread);
        var rain = hours.Any(p => p.Precipitation > HeavyRain);
        var wind = hours.Any(p => p.WindSpeed > StrongWind);

        var risks = new List<string>();
        if (ice)
        {
            risks.Add("Ice");
        }

        if (fog)
        {
            risks.Add("Fog");
        }

        if (rain)
        {
            risks.Add("Heavy rain");
        }

        if (wind)
        {
            risks.Add("Strong wind");
        }

        var level = risks.Count switch
        {
            0 => "Safe",
            1 when !ice => "Caution",
            _ => "Dangerous",
        };
        return [new PipelineEvent.CommuteSafety(current.Location, level, [.. risks])];
    }
}
