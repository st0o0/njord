using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Flood;
using Njord.Forecast.Marine;
using Njord.Forecast.Seasonal;
using Njord.Forecast.Solar;
using Njord.Forecast.Weather;

namespace Njord.Core.Domain;

public readonly record struct WeatherInput(DomainForecast<WeatherPoint> Weather);
public readonly record struct AqInput(DomainForecast<AirQualityPoint> AirQuality);
public readonly record struct WeatherAqInput(DomainForecast<WeatherPoint> Weather, DomainForecast<AirQualityPoint> AirQuality);
public readonly record struct WeatherSolarInput(DomainForecast<WeatherPoint> Weather, DomainForecast<SatelliteRadiationPoint> Satellite);
public readonly record struct FloodWeatherInput(DomainForecast<FloodPoint> Flood, DomainForecast<WeatherPoint> Weather);
public readonly record struct FloodMarineWeatherInput(DomainForecast<FloodPoint> Flood, DomainForecast<MarinePoint> Marine, DomainForecast<WeatherPoint> Weather);
public readonly record struct MarineWeatherInput(DomainForecast<MarinePoint> Marine, DomainForecast<WeatherPoint> Weather);
public readonly record struct SeasonalWeatherInput(DomainForecast<SeasonalPoint> Seasonal, DomainForecast<WeatherPoint> Weather);
