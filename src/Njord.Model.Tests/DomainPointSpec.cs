namespace Njord.Model.Tests;

public sealed class DomainPointSpec
{
    private static readonly DateTimeOffset At = new(2026, 7, 1, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Weather_point_carries_values_and_optional_ensemble_spread()
    {
        var point = new WeatherPoint(At, 20, 19, 3, 0.5, 40, 60, 1013, 3, 55, 10, 4);
        var ensemble = point with { EnsembleStdDev = 1.5 };

        Assert.Multiple(
            () => Assert.Equal(At, point.ValidFrom),
            () => Assert.Equal(20, point.Temperature),
            () => Assert.Equal(19, point.ApparentTemperature),
            () => Assert.Equal(3, point.WindSpeed),
            () => Assert.Equal(0.5, point.Precipitation),
            () => Assert.Equal(40, point.PrecipitationProbability),
            () => Assert.Equal(60, point.CloudCover),
            () => Assert.Equal(1013, point.Pressure),
            () => Assert.Equal(3, point.WeatherCode),
            () => Assert.Equal(55, point.Humidity),
            () => Assert.Equal(10, point.DewPoint),
            () => Assert.Equal(4, point.UvIndex),
            () => Assert.Null(point.EnsembleStdDev),
            () => Assert.Equal(1.5, ensemble.EnsembleStdDev),
            () => Assert.Equal(At, Assert.IsType<WeatherPoint>(point, exactMatch: false).ValidFrom));
    }

    [Fact]
    public void Air_quality_point_has_optional_pollen()
    {
        var point = new AirQualityPoint(At, 12, 20, 80);
        var pollen = point with { BirchPollen = 1, GrassPollen = 2, Alder = 3, Mugwort = 4 };

        Assert.Multiple(
            () => Assert.Equal(At, point.ValidFrom),
            () => Assert.Equal(12, point.Pm25),
            () => Assert.Equal(20, point.Pm10),
            () => Assert.Equal(80, point.Ozone),
            () => Assert.Null(point.BirchPollen),
            () => Assert.Null(point.GrassPollen),
            () => Assert.Null(point.Alder),
            () => Assert.Null(point.Mugwort),
            () => Assert.Equal(1, pollen.BirchPollen),
            () => Assert.Equal(2, pollen.GrassPollen),
            () => Assert.Equal(3, pollen.Alder),
            () => Assert.Equal(4, pollen.Mugwort));
    }

    [Fact]
    public void Flood_point_has_optional_percentiles()
    {
        var point = new FloodPoint(At, 100);
        var full = new FloodPoint(At, 100, 80, 120, 90);

        Assert.Multiple(
            () => Assert.Equal(At, point.ValidFrom),
            () => Assert.Equal(100, point.Discharge),
            () => Assert.Null(point.DischargeP10),
            () => Assert.Null(point.DischargeP90),
            () => Assert.Null(point.MedianHistorical),
            () => Assert.Equal(80, full.DischargeP10),
            () => Assert.Equal(120, full.DischargeP90),
            () => Assert.Equal(90, full.MedianHistorical));
    }

    [Fact]
    public void Solar_point_carries_radiation_values()
    {
        var point = new SolarPoint(At, 300, 100, 400, 1800);

        Assert.Multiple(
            () => Assert.Equal(At, point.ValidFrom),
            () => Assert.Equal(300, point.DirectRadiation),
            () => Assert.Equal(100, point.DiffuseRadiation),
            () => Assert.Equal(400, point.ShortwaveRadiation),
            () => Assert.Equal(1800, point.SunshineDuration));
    }

    [Fact]
    public void Marine_point_has_optional_water_temperature()
    {
        var point = new MarinePoint(At, 1.2, 6, 270);
        var withTemp = point with { WaterTemperature = 17 };

        Assert.Multiple(
            () => Assert.Equal(At, point.ValidFrom),
            () => Assert.Equal(1.2, point.WaveHeight),
            () => Assert.Equal(6, point.WavePeriod),
            () => Assert.Equal(270, point.WaveDirection),
            () => Assert.Null(point.WaterTemperature),
            () => Assert.Equal(17, withTemp.WaterTemperature));
    }

    [Fact]
    public void All_point_types_implement_has_valid_time()
    {
        IHasValidTime[] points =
        [
            new WeatherPoint(At, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            new AirQualityPoint(At, 0, 0, 0),
            new FloodPoint(At, 0),
            new SolarPoint(At, 0, 0, 0, 0),
            new MarinePoint(At, 0, 0, 0)
        ];

        Assert.All(points, p => Assert.Equal(At, p.ValidFrom));
    }
}
