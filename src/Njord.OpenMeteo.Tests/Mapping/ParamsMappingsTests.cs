using Njord.Core.AirQuality;
using Njord.Core.Flood;
using Njord.Core.Forecast;
using Njord.Core.Marine;
using Njord.OpenMeteo.AirQuality;
using Njord.OpenMeteo.Ensemble;
using Njord.OpenMeteo.Flood;
using Njord.OpenMeteo.Forecast;
using Njord.OpenMeteo.Mapping;
using Njord.OpenMeteo.Marine;
using Njord.OpenMeteo.Satellite;
using Njord.OpenMeteo.Seasonal;

namespace Njord.OpenMeteo.Tests.Mapping;

public sealed class ParamsMappingsTests
{
    [Theory]
    [InlineData(ForecastParams.Temperature, "temperature_2m")]
    [InlineData(ForecastParams.ApparentTemperature, "apparent_temperature")]
    [InlineData(ForecastParams.WindSpeed, "wind_speed_10m")]
    [InlineData(ForecastParams.Precipitation, "precipitation")]
    [InlineData(ForecastParams.PrecipitationProbability, "precipitation_probability")]
    [InlineData(ForecastParams.CloudCover, "cloud_cover")]
    [InlineData(ForecastParams.Pressure, "pressure_msl")]
    [InlineData(ForecastParams.WeatherCode, "weather_code")]
    [InlineData(ForecastParams.Humidity, "relative_humidity_2m")]
    [InlineData(ForecastParams.DewPoint, "dew_point_2m")]
    [InlineData(ForecastParams.UvIndex, "uv_index")]
    [InlineData(ForecastParams.WindGusts, "wind_gusts_10m")]
    [InlineData(ForecastParams.Visibility, "visibility")]
    [InlineData(ForecastParams.Cape, "cape")]
    [InlineData(ForecastParams.Rain, "rain")]
    [InlineData(ForecastParams.Snowfall, "snowfall")]
    [InlineData(ForecastParams.SoilTemperature, "soil_temperature_0_to_10cm")]
    [InlineData(ForecastParams.FreezingLevelHeight, "freezing_level_height")]
    [InlineData(ForecastParams.IsDay, "is_day")]
    [InlineData(ForecastParams.ShortwaveRadiation, "shortwave_radiation")]
    [InlineData(ForecastParams.DirectRadiation, "direct_radiation")]
    [InlineData(ForecastParams.DiffuseRadiation, "diffuse_radiation")]
    [InlineData(ForecastParams.DirectNormalIrradiance, "direct_normal_irradiance")]
    [InlineData(ForecastParams.SunshineDuration, "sunshine_duration")]
    [InlineData(ForecastParams.WindDirection, "wind_direction_10m")]
    [InlineData(ForecastParams.GlobalTiltedIrradiance, "global_tilted_irradiance")]
    [InlineData(ForecastParams.SnowDepth, "snow_depth")]
    [InlineData(ForecastParams.WetBulbTemperature, "wet_bulb_temperature_2m")]
    [InlineData(ForecastParams.SnowfallHeight, "snowfall_height")]
    [InlineData(ForecastParams.UvIndexClearSky, "uv_index_clear_sky")]
    public void Should_map_each_weather_flag_to_its_api_name(ForecastParams param, string expected) =>
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
    [InlineData(AirQualityParams.CarbonMonoxide, "carbon_monoxide")]
    [InlineData(AirQualityParams.SulphurDioxide, "sulphur_dioxide")]
    [InlineData(AirQualityParams.Dust, "dust")]
    [InlineData(AirQualityParams.Ammonia, "ammonia")]
    [InlineData(AirQualityParams.OlivePollen, "olive_pollen")]
    [InlineData(AirQualityParams.UsAqi, "us_aqi")]
    [InlineData(AirQualityParams.UvIndex, "uv_index")]
    [InlineData(AirQualityParams.UvIndexClearSky, "uv_index_clear_sky")]
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
    [InlineData(MarineParams.WindWaveHeight, "wind_wave_height")]
    [InlineData(MarineParams.WindWavePeriod, "wind_wave_period")]
    [InlineData(MarineParams.WindWaveDirection, "wind_wave_direction")]
    [InlineData(MarineParams.WindWavePeakPeriod, "wind_wave_peak_period")]
    [InlineData(MarineParams.SwellWaveHeight, "swell_wave_height")]
    [InlineData(MarineParams.SwellWavePeriod, "swell_wave_period")]
    [InlineData(MarineParams.SwellWaveDirection, "swell_wave_direction")]
    [InlineData(MarineParams.SwellWavePeakPeriod, "swell_wave_peak_period")]
    [InlineData(MarineParams.SeaLevelHeight, "sea_level_height_msl")]
    public void Should_map_each_marine_flag_to_its_api_name(MarineParams param, string expected) =>
        Assert.Equal([expected], param.ToApiNames());

    [Theory]
    [InlineData(EnsembleParams.TemperatureSpread, "temperature_2m_spread")]
    [InlineData(EnsembleParams.PrecipitationSpread, "precipitation_spread")]
    [InlineData(EnsembleParams.WindSpeedSpread, "wind_speed_10m_spread")]
    public void Should_map_each_ensemble_flag_to_its_api_name(EnsembleParams param, string expected) =>
        Assert.Equal([expected], param.ToApiNames());

    [Fact]
    public void Should_return_the_names_of_all_set_flags_in_flag_order()
    {
        var names = (ForecastParams.Temperature | ForecastParams.WindDirection | ForecastParams.Rain).ToApiNames();

        Assert.Equal(["temperature_2m", "rain", "wind_direction_10m"], names);
    }

    [Fact]
    public void Should_return_no_names_for_none()
    {
        Assert.Multiple(
            () => Assert.Empty(ForecastParams.None.ToApiNames()),
            () => Assert.Empty(AirQualityParams.None.ToApiNames()),
            () => Assert.Empty(FloodParams.None.ToApiNames()),
            () => Assert.Empty(FloodParams.Ensemble.ToApiNames()),
            () => Assert.Empty(MarineParams.None.ToApiNames()));
    }
}
