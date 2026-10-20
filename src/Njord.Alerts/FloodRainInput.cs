using Njord.Forecast;
using Njord.Forecast.Flood;
using Njord.Forecast.Weather;

namespace Njord.Alerts;

public sealed record FloodRainInput(DomainForecast<FloodPoint> Flood, DomainForecast<WeatherPoint> Weather);
