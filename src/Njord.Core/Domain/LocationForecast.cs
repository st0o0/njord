using System.Collections.Immutable;
using Njord.Core.AirQuality;
using Njord.Core.Flood;
using Njord.Core.Forecast;
using Njord.Core.Marine;
using Njord.Core.Seasonal;

namespace Njord.Core.Domain;

public sealed record LocationForecast(
    Location Location,
    DomainForecast<ForecastPoint>? Forecast = null,
    DomainForecast<AirQualityPoint>? AirQuality = null,
    DomainForecast<FloodPoint>? Flood = null,
    DomainForecast<MarinePoint>? Marine = null,
    DomainForecast<SeasonalPoint>? Seasonal = null,
    DomainForecast<EnsemblePoint>? Ensemble = null,
    ImmutableDictionary<string, DomainForecast<ForecastPoint>>? ModelForecast = null)
{
    public ImmutableDictionary<string, DomainForecast<ForecastPoint>> Models => ModelForecast ?? ImmutableDictionary<string, DomainForecast<ForecastPoint>>.Empty;

    public LocationForecast WithModel(string modelId, DomainForecast<ForecastPoint> forecast) =>
        this with { ModelForecast = Models.SetItem(modelId, forecast) };

    public LocationForecast With(DomainKind domain, IDomainForecast forecast) => domain switch
    {
        DomainKind.Forecast => this with { Forecast = Cast<ForecastPoint>(domain, forecast) },
        DomainKind.AirQuality => this with { AirQuality = Cast<AirQualityPoint>(domain, forecast) },
        DomainKind.Flood => this with { Flood = Cast<FloodPoint>(domain, forecast) },
        DomainKind.Marine => this with { Marine = Cast<MarinePoint>(domain, forecast) },
        DomainKind.Seasonal => this with { Seasonal = Cast<SeasonalPoint>(domain, forecast) },
        DomainKind.Ensemble => this with { Ensemble = Cast<EnsemblePoint>(domain, forecast) },
        _ => throw new ArgumentOutOfRangeException(nameof(domain), domain, null)
    };

    public IDomainForecast? Get(DomainKind domain) => domain switch
    {
        DomainKind.Forecast => Forecast,
        DomainKind.AirQuality => AirQuality,
        DomainKind.Flood => Flood,
        DomainKind.Marine => Marine,
        DomainKind.Seasonal => Seasonal,
        DomainKind.Ensemble => Ensemble,
        _ => throw new ArgumentOutOfRangeException(nameof(domain), domain, null)
    };

    public bool HasFields(DomainRequirements required) =>
        HasFields(Forecast, required.Forecast, static (point, param) => point.HasFields(param))
        && HasFields(AirQuality, required.AirQuality, static (point, param) => point.HasFields(param))
        && HasFields(Flood, required.Flood, static (point, param) => point.HasFields(param))
        && HasFields(Marine, required.Marine, static (point, param) => point.HasFields(param))
        && HasFields(Seasonal, required.Seasonal, static (point, param) => point.HasFields(param))
        && HasFields(Ensemble, required.Ensemble, static (point, param) => point.HasFields(param));

    private static bool HasFields<TPoint, TParam>(DomainForecast<TPoint>? forecast, TParam required, Func<TPoint, TParam, bool> check)
        where TPoint : IHasValidTime
        where TParam : struct, Enum =>
        EqualityComparer<TParam>.Default.Equals(required, default)
        || forecast is { Points.IsDefaultOrEmpty: false } && check(forecast.Points[0], required);

    private static DomainForecast<T> Cast<T>(DomainKind domain, IDomainForecast forecast)
        where T : IHasValidTime =>
        forecast as DomainForecast<T>
        ?? throw new ArgumentException(
            $"Forecast of type {forecast.GetType().Name} does not match domain {domain}.", nameof(forecast));
}
