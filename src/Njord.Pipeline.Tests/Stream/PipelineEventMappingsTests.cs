using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Marine;
using Njord.Forecast.Solar;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Pipeline.Tests;

public sealed class PipelineEventMappingsTests
{
    private static readonly DateTimeOffset Start = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly Location Lucerne = DomainData.Lucerne;

    [Fact]
    public void Weather_data_maps_to_a_weather_data_event()
    {
        var forecast = DomainData.Weather(Start, 2);

        var mapped = new DomainDataReceived(Lucerne, DomainKind.Weather, forecast).ToPipelineEvent();

        var weather = Assert.IsType<PipelineEvent.WeatherDataReceived>(mapped);
        Assert.Multiple(
            () => Assert.Equal(Lucerne, weather.Location),
            () => Assert.Same(forecast, weather.Forecast));
    }

    [Fact]
    public void Flood_data_maps_to_a_flood_data_event()
    {
        var forecast = DomainData.Flood(Start);

        var mapped = new DomainDataReceived(Lucerne, DomainKind.Flood, forecast).ToPipelineEvent();

        var flood = Assert.IsType<PipelineEvent.FloodDataReceived>(mapped);
        Assert.Same(forecast, flood.Forecast);
    }

    [Fact]
    public void Air_quality_and_marine_data_map_to_their_events()
    {
        var airQuality = new DomainForecast<AirQualityPoint>(DomainKind.AirQuality, Start, TimeResolution.Hourly, [new AirQualityPoint(Start, 1, 2, 3)]);
        var marine = new DomainForecast<MarinePoint>(DomainKind.Marine, Start, TimeResolution.Hourly, [new MarinePoint(Start, 1, 2, 3)]);

        Assert.Multiple(
            () => Assert.Same(airQuality, Assert.IsType<PipelineEvent.AirQualityDataReceived>(
                new DomainDataReceived(Lucerne, DomainKind.AirQuality, airQuality).ToPipelineEvent()).Forecast),
            () => Assert.Same(marine, Assert.IsType<PipelineEvent.MarineDataReceived>(
                new DomainDataReceived(Lucerne, DomainKind.Marine, marine).ToPipelineEvent()).Forecast));
    }

    [Fact]
    public void Satellite_data_maps_to_a_satellite_data_event()
    {
        var satellite = new DomainForecast<SatelliteRadiationPoint>(DomainKind.Satellite, Start, TimeResolution.Hourly, [new SatelliteRadiationPoint(Start, GTI: 400)]);

        var mapped = new DomainDataReceived(Lucerne, DomainKind.Satellite, satellite).ToPipelineEvent();

        Assert.Same(satellite, Assert.IsType<PipelineEvent.SatelliteDataReceived>(mapped).Forecast);
    }

    [Fact]
    public void Ensemble_data_maps_to_an_ensemble_data_event()
    {
        var forecast = DomainData.Ensemble(Start);

        var mapped = new DomainDataReceived(Lucerne, DomainKind.Ensemble, forecast).ToPipelineEvent();

        Assert.Same(forecast, Assert.IsType<PipelineEvent.EnsembleDataReceived>(mapped).Forecast);
    }

    [Fact]
    public void Data_of_the_wrong_type_for_its_domain_is_rejected()
    {
        var received = new DomainDataReceived(Lucerne, DomainKind.Marine, DomainData.Flood(Start));

        var ex = Assert.Throws<ArgumentException>(() => received.ToPipelineEvent());

        Assert.Equal("received", ex.ParamName);
    }
}
