using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Flood;
using Njord.Forecast.Marine;
using Njord.Forecast.Seasonal;
using Njord.Forecast.Solar;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;

namespace Njord.Pipeline;

internal static class PipelineEventMappingExtensions
{
    public static PipelineEvent ToPipelineEvent(this DomainDataReceived received) => received.Domain switch
    {
        DomainKind.Weather => new PipelineEvent.WeatherDataReceived(received.Location, Typed<WeatherPoint>(received), received.ModelId),
        DomainKind.AirQuality => new PipelineEvent.AirQualityDataReceived(received.Location, Typed<AirQualityPoint>(received)),
        DomainKind.Flood => new PipelineEvent.FloodDataReceived(received.Location, Typed<FloodPoint>(received)),
        DomainKind.Marine => new PipelineEvent.MarineDataReceived(received.Location, Typed<MarinePoint>(received)),
        DomainKind.Seasonal => new PipelineEvent.SeasonalDataReceived(received.Location, Typed<SeasonalPoint>(received)),
        DomainKind.Satellite => new PipelineEvent.SatelliteDataReceived(received.Location, Typed<SatelliteRadiationPoint>(received)),
        DomainKind.Ensemble when received.ModelId is not null => new PipelineEvent.WeatherDataReceived(received.Location, Typed<WeatherPoint>(received), received.ModelId),
        DomainKind.Ensemble => new PipelineEvent.EnsembleDataReceived(received.Location, Typed<EnsemblePoint>(received)),
        _ => throw new ArgumentOutOfRangeException(nameof(received), received.Domain, null),
    };

    private static DomainForecast<T> Typed<T>(DomainDataReceived received)
        where T : IHasValidTime =>
        received.Data as DomainForecast<T>
        ?? throw new ArgumentException(
            $"Data of type {received.Data.GetType().Name} does not match domain {received.Domain}.", nameof(received));
}
