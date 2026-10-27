using System.Collections.Immutable;
using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Flood;
using Njord.Forecast.Forecast;
using Njord.Forecast.Marine;
using Njord.Forecast.Seasonal;
using Njord.Forecast.Solar;

namespace Njord.Core.Domain;

public readonly record struct ModelComparisonInput(DomainForecast<ForecastPoint> Forecast, ImmutableDictionary<string, DomainForecast<ForecastPoint>> Models);
public readonly record struct ForecastInput(DomainForecast<ForecastPoint> Forecast);
public readonly record struct ForecastEnsembleInput(DomainForecast<ForecastPoint> Forecast, DomainForecast<EnsemblePoint> Ensemble);
public readonly record struct AqInput(DomainForecast<AirQualityPoint> AirQuality);
public readonly record struct ForecastAqInput(DomainForecast<ForecastPoint> Forecast, DomainForecast<AirQualityPoint> AirQuality);
public readonly record struct ForecastSolarInput(DomainForecast<ForecastPoint> Forecast, DomainForecast<SatelliteRadiationPoint> Satellite);
public readonly record struct FloodForecastInput(DomainForecast<FloodPoint> Flood, DomainForecast<ForecastPoint> Forecast);
public readonly record struct FloodMarineForecastInput(DomainForecast<FloodPoint> Flood, DomainForecast<MarinePoint> Marine, DomainForecast<ForecastPoint> Forecast);
public readonly record struct MarineInput(DomainForecast<MarinePoint> Marine);
public readonly record struct MarineForecastInput(DomainForecast<MarinePoint> Marine, DomainForecast<ForecastPoint> Forecast);
public readonly record struct EnsembleInput(DomainForecast<EnsemblePoint> Ensemble);
public readonly record struct SeasonalInput(DomainForecast<SeasonalPoint> Seasonal);
public readonly record struct MarineForecastEnsembleInput(DomainForecast<MarinePoint> Marine, DomainForecast<ForecastPoint> Forecast, DomainForecast<EnsemblePoint> Ensemble);
