using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Flood;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;

namespace Njord.Flood;

public sealed record FloodRainInput(DomainForecast<FloodPoint> Flood, DomainForecast<WeatherPoint> Weather);
