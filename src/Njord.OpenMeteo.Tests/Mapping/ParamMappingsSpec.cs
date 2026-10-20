using Njord.Forecast.AirQuality;
using Njord.Forecast.Flood;
using Njord.Forecast.Marine;
using Njord.Forecast.Weather;
using Njord.OpenMeteo.Mapping;

namespace Njord.OpenMeteo.Tests.Mapping;

public sealed class ParamMappingsSpec
{
    [Theory]
    [InlineData(WeatherParams.Temperature, "temperature_2m")]
    [InlineData(WeatherParams.ApparentTemperature, "apparent_temperature")]
    [InlineData(WeatherParams.WindSpeed, "wind_speed_10m")]
    [InlineData(WeatherParams.Precipitation, "precipitation")]
    [InlineData(WeatherParams.PrecipitationProbability, "precipitation_probability")]
    [InlineData(WeatherParams.CloudCover, "cloud_cover")]
    [InlineData(WeatherParams.Pressure, "pressure_msl")]
    [InlineData(WeatherParams.WeatherCode, "weather_code")]
    [InlineData(WeatherParams.Humidity, "relative_humidity_2m")]
    [InlineData(WeatherParams.DewPoint, "dew_point_2m")]
    [InlineData(WeatherParams.UvIndex, "uv_index")]
    [InlineData(WeatherParams.EnsembleStdDev, "temperature_2m_spread")]
    [InlineData(WeatherParams.WindGusts, "wind_gusts_10m")]
    [InlineData(WeatherParams.Visibility, "visibility")]
    [InlineData(WeatherParams.Cape, "cape")]
    [InlineData(WeatherParams.Rain, "rain")]
    [InlineData(WeatherParams.Snowfall, "snowfall")]
    [InlineData(WeatherParams.SoilTemperature, "soil_temperature_0_to_10cm")]
    [InlineData(WeatherParams.FreezingLevelHeight, "freezing_level_height")]
    [InlineData(WeatherParams.IsDay, "is_day")]
    [InlineData(WeatherParams.ShortwaveRadiation, "shortwave_radiation")]
    [InlineData(WeatherParams.DirectRadiation, "direct_radiation")]
    [InlineData(WeatherParams.DiffuseRadiation, "diffuse_radiation")]
    [InlineData(WeatherParams.DirectNormalIrradiance, "direct_normal_irradiance")]
    [InlineData(WeatherParams.SunshineDuration, "sunshine_duration")]
    [InlineData(WeatherParams.WindDirection, "wind_direction_10m")]
    public void Should_map_each_weather_flag_to_its_api_name(WeatherParams param, string expected) =>
        Assert.Equal([expected], param.ToApiNames());

    [Theory]
    [InlineData(AirQualityParams.Pm25, "pm2_5")]
    [InlineData(AirQualityParams.Pm10, "pm10")]
    [InlineData(AirQualityParams.Ozone, "ozone")]
    [InlineData(AirQualityParams.NitrogenDioxide, "nitrogen_dioxide")]
    [InlineData(AirQualityParams.EuropeanAqi, "european_aqi")]
    [InlineData(AirQualityParams.BirchPollen, "birch_pollen")]
    [InlineData(AirQualityParams.GrassPollen, "grass_pollen")]
    [InlineData(AirQualityParams.Alder, "alder_pollen")]
    [InlineData(AirQualityParams.Mugwort, "mugwort_pollen")]
    public void Should_map_each_air_quality_flag_to_its_api_name(AirQualityParams param, string expected) =>
        Assert.Equal([expected], param.ToApiNames());

    [Theory]
    [InlineData(FloodParams.Discharge, "river_discharge")]
    [InlineData(FloodParams.DischargeMean, "river_discharge_mean")]
    [InlineData(FloodParams.DischargeMedian, "river_discharge_median")]
    [InlineData(FloodParams.DischargeMax, "river_discharge_max")]
    [InlineData(FloodParams.DischargeMin, "river_discharge_min")]
    [InlineData(FloodParams.DischargeP25, "river_discharge_p25")]
    [InlineData(FloodParams.DischargeP75, "river_discharge_p75")]
    public void Should_map_each_flood_flag_to_its_api_name(FloodParams param, string expected) =>
        Assert.Equal([expected], param.ToApiNames());

    [Theory]
    [InlineData(MarineParams.WaveHeight, "wave_height")]
    [InlineData(MarineParams.WavePeriod, "wave_period")]
    [InlineData(MarineParams.WaveDirection, "wave_direction")]
    [InlineData(MarineParams.WaterTemperature, "sea_surface_temperature")]
    public void Should_map_each_marine_flag_to_its_api_name(MarineParams param, string expected) =>
        Assert.Equal([expected], param.ToApiNames());

    [Fact]
    public void Should_return_the_names_of_all_set_flags_in_flag_order()
    {
        var names = (WeatherParams.Temperature | WeatherParams.WindDirection | WeatherParams.Rain).ToApiNames();

        Assert.Equal(["temperature_2m", "rain", "wind_direction_10m"], names);
    }

    [Fact]
    public void Should_return_no_names_for_none()
    {
        Assert.Multiple(
            () => Assert.Empty(WeatherParams.None.ToApiNames()),
            () => Assert.Empty(AirQualityParams.None.ToApiNames()),
            () => Assert.Empty(FloodParams.None.ToApiNames()),
            () => Assert.Empty(MarineParams.None.ToApiNames()));
    }
}
