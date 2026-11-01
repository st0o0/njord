using Njord.Core.Forecast;
using Njord.Grpc.Mapping;

namespace Njord.Grpc.Tests;

public sealed class SampleMappingsTests
{
    private static readonly DateTimeOffset At = new(2026, 7, 1, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Forecast_extended_values_map_to_their_own_fields()
    {
        var sample = new ForecastPoint(At, WindDirection: 1, SnowDepth: 2, ShortwaveRadiation: 3, DirectRadiation: 4,
            DiffuseRadiation: 5, DirectNormalIrradiance: 6, SunshineDuration: 7, GlobalTiltedIrradiance: 8,
            WetBulbTemperature: 9, SnowfallHeight: 10, WindSpeed100m: 11, WindDirection100m: 12,
            ThunderstormProbability: 13, PrecipitationType: 14, SurfaceTemperature: 15,
            SnowfallWaterEquivalent: 16).ToProto();

        Assert.Multiple(
            () => Assert.Equal(1, sample.WindDirection10M),
            () => Assert.Equal(2, sample.SnowDepth),
            () => Assert.Equal(3, sample.ShortwaveRadiation),
            () => Assert.Equal(4, sample.DirectRadiation),
            () => Assert.Equal(5, sample.DiffuseRadiation),
            () => Assert.Equal(6, sample.DirectNormalIrradiance),
            () => Assert.Equal(7, sample.SunshineDuration),
            () => Assert.Equal(8, sample.GlobalTiltedIrradiance),
            () => Assert.Equal(9, sample.WetBulbTemperature2M),
            () => Assert.Equal(10, sample.SnowfallHeight),
            () => Assert.Equal(11, sample.WindSpeed100M),
            () => Assert.Equal(12, sample.WindDirection100M),
            () => Assert.Equal(13, sample.ThunderstormProbability),
            () => Assert.Equal(14, sample.PrecipitationType),
            () => Assert.Equal(15, sample.SurfaceTemperature),
            () => Assert.Equal(16, sample.SnowfallWaterEquivalent));
    }

    [Fact]
    public void Missing_forecast_extended_values_stay_unset()
    {
        var sample = new ForecastPoint(At, Temperature: 20).ToProto();

        Assert.Multiple(
            () => Assert.False(sample.HasWindDirection10M),
            () => Assert.False(sample.HasSnowDepth),
            () => Assert.False(sample.HasSurfaceTemperature));
    }

    [Fact]
    public void Ensemble_spread_values_map_to_their_own_fields()
    {
        var sample = new EnsemblePoint(At, PressureMslSpread: 1, CloudCoverSpread: 2, WindGustsSpread: 3,
            CapeSpread: 4, SnowfallSpread: 5, RainSpread: 6, DewPointSpread: 7, HumiditySpread: 8,
            SurfacePressureSpread: 9, WindDirectionSpread: 10, WetBulbTemperatureSpread: 11,
            WindSpeed100mSpread: 12).ToProto();

        Assert.Multiple(
            () => Assert.Equal(1, sample.PressureMslSpread),
            () => Assert.Equal(2, sample.CloudCoverSpread),
            () => Assert.Equal(3, sample.WindGusts10MSpread),
            () => Assert.Equal(4, sample.CapeSpread),
            () => Assert.Equal(5, sample.SnowfallSpread),
            () => Assert.Equal(6, sample.RainSpread),
            () => Assert.Equal(7, sample.DewPoint2MSpread),
            () => Assert.Equal(8, sample.RelativeHumidity2MSpread),
            () => Assert.Equal(9, sample.SurfacePressureSpread),
            () => Assert.Equal(10, sample.WindDirection10MSpread),
            () => Assert.Equal(11, sample.WetBulbTemperature2MSpread),
            () => Assert.Equal(12, sample.WindSpeed100MSpread));
    }
}
