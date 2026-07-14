using Njord.Domain.Weather;

namespace Njord.Tests.Domain.Weather;

public sealed class ParameterRegistryTypedAccessSpec
{
    [Fact(Timeout = 5000)]
    public void All_typed_properties_are_non_null()
    {
        Assert.NotNull(ParameterRegistry.Temperature2m);
        Assert.NotNull(ParameterRegistry.ApparentTemperature);
        Assert.NotNull(ParameterRegistry.RelativeHumidity2m);
        Assert.NotNull(ParameterRegistry.DewPoint2m);
        Assert.NotNull(ParameterRegistry.WindSpeed10m);
        Assert.NotNull(ParameterRegistry.WindGusts10m);
        Assert.NotNull(ParameterRegistry.Precipitation);
        Assert.NotNull(ParameterRegistry.PrecipitationProbability);
        Assert.NotNull(ParameterRegistry.CloudCover);
        Assert.NotNull(ParameterRegistry.PressureMsl);
        Assert.NotNull(ParameterRegistry.SurfacePressure);
        Assert.NotNull(ParameterRegistry.ShortwaveRadiation);
        Assert.NotNull(ParameterRegistry.SunshineDuration);
        Assert.NotNull(ParameterRegistry.UvIndex);
        Assert.NotNull(ParameterRegistry.IsDay);
        Assert.NotNull(ParameterRegistry.Snowfall);
        Assert.NotNull(ParameterRegistry.FreezingLevelHeight);
        Assert.NotNull(ParameterRegistry.Cape);
        Assert.NotNull(ParameterRegistry.WeatherCode);
        Assert.NotNull(ParameterRegistry.Et0FaoEvapotranspiration);
    }

    [Fact(Timeout = 5000)]
    public void Typed_property_returns_correct_api_name()
    {
        Assert.Equal("temperature_2m", ParameterRegistry.Temperature2m.ApiName);
        Assert.Equal("wind_speed_10m", ParameterRegistry.WindSpeed10m.ApiName);
        Assert.Equal("cloud_cover", ParameterRegistry.CloudCover.ApiName);
    }

    [Fact(Timeout = 5000)]
    public void Typed_property_matches_GetByApiName_result()
    {
        Assert.Equal(
            ParameterRegistry.GetByApiName("temperature_2m"),
            ParameterRegistry.Temperature2m);
    }
}
