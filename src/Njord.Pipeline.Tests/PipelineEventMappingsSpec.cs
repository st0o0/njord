using Njord.Core.Domain;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Tests.Shared;

namespace Njord.Pipeline.Tests;

public sealed class PipelineEventMappingsSpec
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
    public void Air_quality_solar_and_marine_data_map_to_their_events()
    {
        var airQuality = new DomainForecast<AirQualityPoint>(DomainKind.AirQuality, Start, TimeResolution.Hourly, [new AirQualityPoint(Start, 1, 2, 3)]);
        var solar = new DomainForecast<SolarPoint>(DomainKind.Solar, Start, TimeResolution.Hourly, [new SolarPoint(Start, 1, 2, 3, 4)]);
        var marine = new DomainForecast<MarinePoint>(DomainKind.Marine, Start, TimeResolution.Hourly, [new MarinePoint(Start, 1, 2, 3)]);

        Assert.Multiple(
            () => Assert.Same(airQuality, Assert.IsType<PipelineEvent.AirQualityDataReceived>(
                new DomainDataReceived(Lucerne, DomainKind.AirQuality, airQuality).ToPipelineEvent()).Forecast),
            () => Assert.Same(solar, Assert.IsType<PipelineEvent.SolarDataReceived>(
                new DomainDataReceived(Lucerne, DomainKind.Solar, solar).ToPipelineEvent()).Forecast),
            () => Assert.Same(marine, Assert.IsType<PipelineEvent.MarineDataReceived>(
                new DomainDataReceived(Lucerne, DomainKind.Marine, marine).ToPipelineEvent()).Forecast));
    }

    [Fact]
    public void Data_of_the_wrong_type_for_its_domain_is_rejected()
    {
        var received = new DomainDataReceived(Lucerne, DomainKind.Marine, DomainData.Flood(Start));

        var ex = Assert.Throws<ArgumentException>(() => received.ToPipelineEvent());

        Assert.Equal("received", ex.ParamName);
    }
}
