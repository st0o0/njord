using Njord.Core;
using Njord.Core.AirQuality;
using Njord.Core.Flood;
using Njord.Core.Forecast;
using Njord.Core.Marine;
using Njord.Core.Seasonal;
using Njord.Core.Solar;
using Njord.Messages.Stream;

namespace Njord.Pipeline.Stream;

internal static class PipelineEventMappingExtensions
{
    public static PipelineEvent ToPipelineEvent(this DomainDataReceived received) => received.Domain switch
    {
        DomainKind.Forecast => new ForecastDataReceived(received.Location, Typed<ForecastPoint>(received), received.ModelId),
        DomainKind.AirQuality => new AirQualityDataReceived(received.Location, Typed<AirQualityPoint>(received)),
        DomainKind.Flood => new FloodDataReceived(received.Location, Typed<FloodPoint>(received)),
        DomainKind.Marine => new MarineDataReceived(received.Location, Typed<MarinePoint>(received)),
        DomainKind.Seasonal => new SeasonalDataReceived(received.Location, Typed<SeasonalPoint>(received)),
        DomainKind.Satellite => new SatelliteDataReceived(received.Location, Typed<SatelliteRadiationPoint>(received)),
        DomainKind.Ensemble when received.ModelId is not null => new ForecastDataReceived(received.Location, Typed<ForecastPoint>(received), received.ModelId),
        DomainKind.Ensemble => new EnsembleDataReceived(received.Location, Typed<EnsemblePoint>(received)),
        _ => throw new ArgumentOutOfRangeException(nameof(received), received.Domain, null),
    };

    private static DomainForecast<T> Typed<T>(DomainDataReceived received)
        where T : IHasValidTime =>
        received.Data as DomainForecast<T>
        ?? throw new ArgumentException(
            $"Data of type {received.Data.GetType().Name} does not match domain {received.Domain}.", nameof(received));
}
