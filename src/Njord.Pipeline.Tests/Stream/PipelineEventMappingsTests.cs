using Njord.Core;
using Njord.Core.AirQuality;
using Njord.Core.Forecast;
using Njord.Core.Marine;
using Njord.Messages.Stream;
using Njord.Pipeline.Stream;
using Njord.Tests.Shared;

namespace Njord.Pipeline.Tests.Stream;

public sealed class PipelineEventMappingsTests
{
    private static readonly DateTimeOffset _start = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly Location _lucerne = DomainData.Lucerne;

    [Fact]
    public void Weather_data_maps_to_a_weather_data_event()
    {
        var forecast = DomainData.Forecast(_start, 2);

        var mapped = new DomainDataReceived(_lucerne, DomainKind.Forecast, forecast).ToPipelineEvent();

        var weather = Assert.IsType<ForecastDataReceived>(mapped);
        Assert.Multiple(
            () => Assert.Equal(_lucerne, weather.Location),
            () => Assert.Same(forecast, weather.Forecast));
    }

    [Fact]
    public void Flood_data_maps_to_a_flood_data_event()
    {
        var forecast = DomainData.Flood(_start);

        var mapped = new DomainDataReceived(_lucerne, DomainKind.Flood, forecast).ToPipelineEvent();

        var flood = Assert.IsType<FloodDataReceived>(mapped);
        Assert.Same(forecast, flood.Forecast);
    }

    [Fact]
    public void Air_quality_and_marine_data_map_to_their_events()
    {
        var airQuality = new DomainForecast<AirQualityPoint>(DomainKind.AirQuality, _start, TimeResolution.Hourly, [new AirQualityPoint(_start, 1, 2, 3)]);
        var marine = new DomainForecast<MarinePoint>(DomainKind.Marine, _start, TimeResolution.Hourly, [new MarinePoint(_start, 1, 2, 3)]);

        Assert.Multiple(
            () => Assert.Same(airQuality, Assert.IsType<AirQualityDataReceived>(
                new DomainDataReceived(_lucerne, DomainKind.AirQuality, airQuality).ToPipelineEvent()).Forecast),
            () => Assert.Same(marine, Assert.IsType<MarineDataReceived>(
                new DomainDataReceived(_lucerne, DomainKind.Marine, marine).ToPipelineEvent()).Forecast));
    }

    [Fact]
    public void Ensemble_data_maps_to_an_ensemble_data_event()
    {
        var forecast = DomainData.Ensemble(_start);

        var mapped = new DomainDataReceived(_lucerne, DomainKind.Ensemble, forecast).ToPipelineEvent();

        Assert.Same(forecast, Assert.IsType<EnsembleDataReceived>(mapped).Forecast);
    }

    [Fact]
    public void Data_of_the_wrong_type_for_its_domain_is_rejected()
    {
        var received = new DomainDataReceived(_lucerne, DomainKind.Marine, DomainData.Flood(_start));

        var ex = Assert.Throws<ArgumentException>(() => received.ToPipelineEvent());

        Assert.Equal("received", ex.ParamName);
    }
}
