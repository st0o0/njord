using Njord.Forecast.AirQuality;
using Njord.Forecast.Flood;
using Njord.Forecast.Marine;
using Njord.Forecast.Weather;
using Njord.OpenMeteo.Mapping;

namespace Njord.OpenMeteo.Tests.Mapping;

public sealed class ParamMappingsSpec
{
    [Theory]
    [InlineData(WeatherParam.Temperature, "temperature_2m")]
    [InlineData(WeatherParam.ApparentTemperature, "apparent_temperature")]
    [InlineData(WeatherParam.WindSpeed, "wind_speed_10m")]
    [InlineData(WeatherParam.Precipitation, "precipitation")]
    [InlineData(WeatherParam.PrecipitationProbability, "precipitation_probability")]
    [InlineData(WeatherParam.CloudCover, "cloud_cover")]
    [InlineData(WeatherParam.Pressure, "pressure_msl")]
    [InlineData(WeatherParam.WeatherCode, "weather_code")]
    [InlineData(WeatherParam.Humidity, "relative_humidity_2m")]
    [InlineData(WeatherParam.DewPoint, "dew_point_2m")]
    [InlineData(WeatherParam.UvIndex, "uv_index")]
    [InlineData(WeatherParam.EnsembleStdDev, "temperature_2m_spread")]
    [InlineData(WeatherParam.WindGusts, "wind_gusts_10m")]
    [InlineData(WeatherParam.Visibility, "visibility")]
    [InlineData(WeatherParam.Cape, "cape")]
    [InlineData(WeatherParam.Rain, "rain")]
    [InlineData(WeatherParam.Snowfall, "snowfall")]
    [InlineData(WeatherParam.SoilTemperature, "soil_temperature_0_to_10cm")]
    [InlineData(WeatherParam.FreezingLevelHeight, "freezing_level_height")]
    [InlineData(WeatherParam.IsDay, "is_day")]
    [InlineData(WeatherParam.ShortwaveRadiation, "shortwave_radiation")]
    [InlineData(WeatherParam.DirectRadiation, "direct_radiation")]
    [InlineData(WeatherParam.DiffuseRadiation, "diffuse_radiation")]
    [InlineData(WeatherParam.DirectNormalIrradiance, "direct_normal_irradiance")]
    [InlineData(WeatherParam.SunshineDuration, "sunshine_duration")]
    [InlineData(WeatherParam.WindDirection, "wind_direction_10m")]
    public void Should_map_each_weather_flag_to_its_api_name(WeatherParam param, string expected) =>
        Assert.Equal([expected], param.ToApiNames());

    [Theory]
    [InlineData(AirQualityParam.Pm25, "pm2_5")]
    [InlineData(AirQualityParam.Pm10, "pm10")]
    [InlineData(AirQualityParam.Ozone, "ozone")]
    [InlineData(AirQualityParam.NitrogenDioxide, "nitrogen_dioxide")]
    [InlineData(AirQualityParam.EuropeanAqi, "european_aqi")]
    [InlineData(AirQualityParam.BirchPollen, "birch_pollen")]
    [InlineData(AirQualityParam.GrassPollen, "grass_pollen")]
    [InlineData(AirQualityParam.Alder, "alder_pollen")]
    [InlineData(AirQualityParam.Mugwort, "mugwort_pollen")]
    public void Should_map_each_air_quality_flag_to_its_api_name(AirQualityParam param, string expected) =>
        Assert.Equal([expected], param.ToApiNames());

    [Theory]
    [InlineData(FloodParam.Discharge, "river_discharge")]
    [InlineData(FloodParam.DischargeMean, "river_discharge_mean")]
    [InlineData(FloodParam.DischargeMedian, "river_discharge_median")]
    [InlineData(FloodParam.DischargeMax, "river_discharge_max")]
    [InlineData(FloodParam.DischargeMin, "river_discharge_min")]
    [InlineData(FloodParam.DischargeP25, "river_discharge_p25")]
    [InlineData(FloodParam.DischargeP75, "river_discharge_p75")]
    public void Should_map_each_flood_flag_to_its_api_name(FloodParam param, string expected) =>
        Assert.Equal([expected], param.ToApiNames());

    [Theory]
    [InlineData(MarineParam.WaveHeight, "wave_height")]
    [InlineData(MarineParam.WavePeriod, "wave_period")]
    [InlineData(MarineParam.WaveDirection, "wave_direction")]
    [InlineData(MarineParam.WaterTemperature, "sea_surface_temperature")]
    public void Should_map_each_marine_flag_to_its_api_name(MarineParam param, string expected) =>
        Assert.Equal([expected], param.ToApiNames());

    [Fact]
    public void Should_return_the_names_of_all_set_flags_in_flag_order()
    {
        var names = (WeatherParam.Temperature | WeatherParam.WindDirection | WeatherParam.Rain).ToApiNames();

        Assert.Equal(["temperature_2m", "rain", "wind_direction_10m"], names);
    }

    [Fact]
    public void Should_return_no_names_for_none()
    {
        Assert.Multiple(
            () => Assert.Empty(WeatherParam.None.ToApiNames()),
            () => Assert.Empty(AirQualityParam.None.ToApiNames()),
            () => Assert.Empty(FloodParam.None.ToApiNames()),
            () => Assert.Empty(MarineParam.None.ToApiNames()));
    }
}
