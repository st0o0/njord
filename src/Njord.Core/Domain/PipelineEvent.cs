using Njord.Core.AirQuality;
using Njord.Core.Flood;
using Njord.Core.Forecast;
using Njord.Core.Marine;
using Njord.Core.Seasonal;
using Njord.Core.Solar;

namespace Njord.Core.Domain;

public abstract record PipelineEvent
{
    public sealed record ForecastDataReceived(
        Location Location,
        DomainForecast<ForecastPoint> Forecast,
        string? ModelId = null) : PipelineEvent;

    public sealed record AirQualityDataReceived(
        Location Location,
        DomainForecast<AirQualityPoint> Forecast) : PipelineEvent;

    public sealed record FloodDataReceived(
        Location Location,
        DomainForecast<FloodPoint> Forecast) : PipelineEvent;

    public sealed record MarineDataReceived(
        Location Location,
        DomainForecast<MarinePoint> Forecast) : PipelineEvent;

    public sealed record SeasonalDataReceived(
        Location Location,
        DomainForecast<SeasonalPoint> Forecast) : PipelineEvent;

    public sealed record SatelliteDataReceived(
        Location Location,
        DomainForecast<SatelliteRadiationPoint> Forecast) : PipelineEvent;

    public sealed record EnsembleDataReceived(
        Location Location,
        DomainForecast<EnsemblePoint> Forecast) : PipelineEvent;

    public sealed record Heartbeat : PipelineEvent
    {
        public static readonly Heartbeat Instance = new();
    }
}
