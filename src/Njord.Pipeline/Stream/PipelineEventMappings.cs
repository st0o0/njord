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

internal static class PipelineEventMappings
{
    public static PipelineEvent ToPipelineEvent(this DomainDataReceived received) => received.Domain switch
    {
        DomainKind.Weather => new PipelineEvent.WeatherDataReceived(received.Location, Typed<WeatherPoint>(received)),
        DomainKind.AirQuality => new PipelineEvent.AirQualityDataReceived(received.Location, Typed<AirQualityPoint>(received)),
        DomainKind.Flood => new PipelineEvent.FloodDataReceived(received.Location, Typed<FloodPoint>(received)),
        DomainKind.Solar => new PipelineEvent.SolarDataReceived(received.Location, Typed<SolarPoint>(received)),
        DomainKind.Marine => new PipelineEvent.MarineDataReceived(received.Location, Typed<MarinePoint>(received)),
        DomainKind.Seasonal => new PipelineEvent.SeasonalDataReceived(received.Location, Typed<SeasonalPoint>(received)),
        _ => throw new ArgumentOutOfRangeException(nameof(received), received.Domain, null),
    };

    private static DomainForecast<T> Typed<T>(DomainDataReceived received)
        where T : IHasValidTime =>
        received.Data as DomainForecast<T>
        ?? throw new ArgumentException(
            $"Data of type {received.Data.GetType().Name} does not match domain {received.Domain}.", nameof(received));
}
