using Njord.Grpc.Mapping;
using Njord.Grpc.V2;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public sealed class DomainDataProtoMappingsSpec
{
    private static readonly DateTimeOffset Start = TestTimeConstants.BudgetEpoch;

    [Fact]
    public void Weather_events_map_every_point_value_and_the_optional_ones_only_when_present()
    {
        var forecast = DomainData.Weather(Start, 2, (hour, point) =>
            hour == 1 ? point with { WindGusts = 12.5, IsDay = true } : point);

        var update = new PipelineEvent.WeatherDataReceived(DomainData.Lucerne, forecast).ToDomainDataUpdate();

        Assert.NotNull(update);
        Assert.Multiple(
            () => Assert.Equal(("Lucerne", Domain.Weather, Start), (update.Location, update.Domain, update.ModelRun.ToDateTimeOffset())),
            () => Assert.Equal(DomainDataUpdate.DataOneofCase.Weather, update.DataCase),
            () => Assert.Equal(2, update.Weather.Points.Count));
        var first = update.Weather.Points[0];
        var second = update.Weather.Points[1];
        Assert.Multiple(
            () => Assert.Equal(Start, first.ValidAt.ToDateTimeOffset()),
            () => Assert.Equal((10.0, 10.0, 2.0, 0.0, 0.0), (first.Temperature, first.ApparentTemperature, first.WindSpeed, first.Precipitation, first.PrecipitationProbability)),
            () => Assert.Equal((50.0, 1013.0, 0, 60.0, 5.0, 0.0), (first.CloudCover, first.Pressure, first.WeatherCode, first.Humidity, first.DewPoint, first.UvIndex)),
            () => Assert.True(first.HasEnsembleStdDev),
            () => Assert.Equal(0.5, first.EnsembleStdDev),
            () => Assert.False(first.HasWindGusts),
            () => Assert.False(first.HasIsDay),
            () => Assert.True(second.HasWindGusts),
            () => Assert.Equal(12.5, second.WindGusts),
            () => Assert.True(second.IsDay));
    }

    [Fact]
    public void Air_quality_events_map_pollen_only_when_present()
    {
        var forecast = new DomainForecast<AirQualityPoint>(DomainKind.AirQuality, Start, TimeResolution.Hourly,
            [new AirQualityPoint(Start, 8.5, 12.0, 60.0, BirchPollen: 3.0)]);

        var update = new PipelineEvent.AirQualityDataReceived(DomainData.Lucerne, forecast).ToDomainDataUpdate();

        Assert.NotNull(update);
        var point = Assert.Single(update.AirQuality.Points);
        Assert.Multiple(
            () => Assert.Equal(Domain.AirQuality, update.Domain),
            () => Assert.Equal((8.5, 12.0, 60.0), (point.Pm25, point.Pm10, point.Ozone)),
            () => Assert.Equal(3.0, point.BirchPollen),
            () => Assert.False(point.HasGrassPollen),
            () => Assert.False(point.HasAlder),
            () => Assert.False(point.HasMugwort));
    }

    [Fact]
    public void Flood_events_map_discharge_and_optional_percentiles()
    {
        var forecast = new DomainForecast<FloodPoint>(DomainKind.Flood, Start, TimeResolution.Daily,
            [new FloodPoint(Start, 12.5, DischargeP75: 20.0)]);

        var update = new PipelineEvent.FloodDataReceived(DomainData.Lucerne, forecast).ToDomainDataUpdate();

        Assert.NotNull(update);
        var point = Assert.Single(update.Flood.Points);
        Assert.Multiple(
            () => Assert.Equal(Domain.Flood, update.Domain),
            () => Assert.Equal(12.5, point.Discharge),
            () => Assert.False(point.HasDischargeMedian),
            () => Assert.Equal(20.0, point.DischargeP75),
            () => Assert.False(point.HasDischargeMean));
    }

    [Fact]
    public void Solar_events_map_all_radiation_values()
    {
        var forecast = new DomainForecast<SolarPoint>(DomainKind.Solar, Start, TimeResolution.Hourly,
            [new SolarPoint(Start, 410.0, 90.0, 500.0, 3300.0)]);

        var update = new PipelineEvent.SolarDataReceived(DomainData.Lucerne, forecast).ToDomainDataUpdate();

        Assert.NotNull(update);
        var point = Assert.Single(update.Solar.Points);
        Assert.Equal((Domain.Solar, 410.0, 90.0, 500.0, 3300.0),
            (update.Domain, point.DirectRadiation, point.DiffuseRadiation, point.ShortwaveRadiation, point.SunshineDuration));
    }

    [Fact]
    public void Marine_events_map_waves_and_the_optional_water_temperature()
    {
        var forecast = new DomainForecast<MarinePoint>(DomainKind.Marine, Start, TimeResolution.Hourly,
            [new MarinePoint(Start, 0.8, 5.5, 220.0), new MarinePoint(Start.AddHours(1), 0.9, 5.0, 210.0, 17.5)]);

        var update = new PipelineEvent.MarineDataReceived(DomainData.Lucerne, forecast).ToDomainDataUpdate();

        Assert.NotNull(update);
        Assert.Multiple(
            () => Assert.Equal(Domain.Marine, update.Domain),
            () => Assert.Equal((0.8, 5.5, 220.0), (update.Marine.Points[0].WaveHeight, update.Marine.Points[0].WavePeriod, update.Marine.Points[0].WaveDirection)),
            () => Assert.False(update.Marine.Points[0].HasWaterTemperature),
            () => Assert.Equal(17.5, update.Marine.Points[1].WaterTemperature));
    }

    [Fact]
    public void Events_without_domain_data_map_to_null()
    {
        var alert = EnrichmentEvents.Alerts(new Location("lucerne", 0, 0), Start);

        Assert.Multiple(
            () => Assert.Null(alert.ToDomainDataUpdate()),
            () => Assert.Null(alert.ToDomainKind()),
            () => Assert.Null(alert.ToDomainLocation()));
    }

    [Theory]
    [InlineData(DomainKind.Weather)]
    [InlineData(DomainKind.AirQuality)]
    [InlineData(DomainKind.Flood)]
    [InlineData(DomainKind.Solar)]
    [InlineData(DomainKind.Marine)]
    public void Domain_events_report_their_domain_and_location(DomainKind domain)
    {
        PipelineEvent evt = domain switch
        {
            DomainKind.Weather => new PipelineEvent.WeatherDataReceived(DomainData.Lucerne, DomainData.Weather(Start, 1)),
            DomainKind.AirQuality => new PipelineEvent.AirQualityDataReceived(DomainData.Lucerne,
                new DomainForecast<AirQualityPoint>(domain, Start, TimeResolution.Hourly, [new AirQualityPoint(Start, 1, 2, 3)])),
            DomainKind.Flood => new PipelineEvent.FloodDataReceived(DomainData.Lucerne, DomainData.Flood(Start)),
            DomainKind.Solar => new PipelineEvent.SolarDataReceived(DomainData.Lucerne,
                new DomainForecast<SolarPoint>(domain, Start, TimeResolution.Hourly, [new SolarPoint(Start, 1, 2, 3, 4)])),
            DomainKind.Marine => new PipelineEvent.MarineDataReceived(DomainData.Lucerne,
                new DomainForecast<MarinePoint>(domain, Start, TimeResolution.Hourly, [new MarinePoint(Start, 1, 2, 3)])),
            _ => throw new ArgumentOutOfRangeException(nameof(domain)),
        };

        Assert.Multiple(
            () => Assert.Equal(domain, evt.ToDomainKind()),
            () => Assert.Equal(new Location("Lucerne", 0, 0), evt.ToDomainLocation()),
            () => Assert.Equal(domain.ToProto(), evt.ToDomainDataUpdate()?.Domain));
    }
}
