using Njord.Core.Configuration;
using Njord.Mqtt.Mapping;

namespace Njord.Mqtt.Tests.Mapping;

public sealed class ParameterMqttKeysSpec
{
    // The payload keys as they were published before the key left ParameterDef (hourly "h:", daily "d:").
    private static readonly Dictionary<string, string> FormerKeys = new()
    {
        ["h:temperature_2m"] = "temperature",
        ["h:apparent_temperature"] = "apparent_temperature",
        ["h:relative_humidity_2m"] = "relative_humidity",
        ["h:dew_point_2m"] = "dewpoint",
        ["h:precipitation"] = "precipitation",
        ["h:rain"] = "rain",
        ["h:showers"] = "showers",
        ["h:snowfall"] = "snowfall",
        ["h:snow_depth"] = "snow_depth",
        ["h:weather_code"] = "weather_code",
        ["h:cloud_cover"] = "cloud_cover",
        ["h:cloud_cover_low"] = "cloud_cover_low",
        ["h:cloud_cover_mid"] = "cloud_cover_mid",
        ["h:cloud_cover_high"] = "cloud_cover_high",
        ["h:pressure_msl"] = "pressure_msl",
        ["h:surface_pressure"] = "surface_pressure",
        ["h:visibility"] = "visibility",
        ["h:is_day"] = "is_day",
        ["h:precipitation_probability"] = "precipitation_probability",
        ["h:wind_speed_10m"] = "wind_speed_10m",
        ["h:wind_speed_80m"] = "wind_speed_80m",
        ["h:wind_speed_120m"] = "wind_speed_120m",
        ["h:wind_speed_180m"] = "wind_speed_180m",
        ["h:wind_direction_10m"] = "wind_direction_10m",
        ["h:wind_direction_80m"] = "wind_direction_80m",
        ["h:wind_direction_120m"] = "wind_direction_120m",
        ["h:wind_direction_180m"] = "wind_direction_180m",
        ["h:wind_gusts_10m"] = "wind_gusts_10m",
        ["h:cape"] = "cape",
        ["h:freezing_level_height"] = "freezing_level_height",
        ["h:vapour_pressure_deficit"] = "vapour_pressure_deficit",
        ["d:temperature_2m_max"] = "temperature_max",
        ["d:temperature_2m_min"] = "temperature_min",
        ["d:apparent_temperature_max"] = "apparent_temperature_max",
        ["d:apparent_temperature_min"] = "apparent_temperature_min",
        ["d:weather_code"] = "weather_code_daily",
        ["d:precipitation_sum"] = "precipitation_sum",
        ["d:rain_sum"] = "rain_sum",
        ["d:showers_sum"] = "showers_sum",
        ["d:snowfall_sum"] = "snowfall_sum",
        ["d:precipitation_hours"] = "precipitation_hours",
        ["d:precipitation_probability_max"] = "precipitation_probability_max",
        ["d:wind_speed_10m_max"] = "wind_speed_max",
        ["d:wind_gusts_10m_max"] = "wind_gusts_max",
        ["d:wind_direction_10m_dominant"] = "wind_direction_dominant",
        ["d:sunrise"] = "sunrise",
        ["d:sunset"] = "sunset",
        ["d:daylight_duration"] = "daylight_duration",
        ["h:shortwave_radiation"] = "shortwave_radiation",
        ["h:direct_radiation"] = "direct_radiation",
        ["h:diffuse_radiation"] = "diffuse_radiation",
        ["h:direct_normal_irradiance"] = "direct_normal_irradiance",
        ["h:global_tilted_irradiance"] = "global_tilted_irradiance",
        ["h:terrestrial_radiation"] = "terrestrial_radiation",
        ["h:sunshine_duration"] = "sunshine_duration",
        ["h:uv_index"] = "uv_index",
        ["h:uv_index_clear_sky"] = "uv_index_clear_sky",
        ["d:shortwave_radiation_sum"] = "shortwave_radiation_sum",
        ["d:sunshine_duration"] = "sunshine_duration_daily",
        ["d:uv_index_max"] = "uv_index_max",
        ["d:uv_index_clear_sky_max"] = "uv_index_clear_sky_max",
        ["h:soil_temperature_0cm"] = "soil_temperature_0cm",
        ["h:soil_temperature_6cm"] = "soil_temperature_6cm",
        ["h:soil_temperature_18cm"] = "soil_temperature_18cm",
        ["h:soil_temperature_54cm"] = "soil_temperature_54cm",
        ["h:soil_moisture_0_to_1cm"] = "soil_moisture_0_to_1cm",
        ["h:soil_moisture_1_to_3cm"] = "soil_moisture_1_to_3cm",
        ["h:soil_moisture_3_to_9cm"] = "soil_moisture_3_to_9cm",
        ["h:soil_moisture_9_to_27cm"] = "soil_moisture_9_to_27cm",
        ["h:soil_moisture_27_to_81cm"] = "soil_moisture_27_to_81cm",
        ["h:evapotranspiration"] = "evapotranspiration",
        ["h:et0_fao_evapotranspiration"] = "et0_fao_evapotranspiration",
        ["d:et0_fao_evapotranspiration"] = "et0_fao_evapotranspiration_daily",
    };

    private static string Id(ParameterDef parameter)
        => (parameter.Granularity == ParameterGranularity.Hourly ? "h:" : "d:") + parameter.ApiName;

    [Fact]
    public void The_former_key_table_covers_the_whole_catalog()
    {
        Assert.Equal(
            ParameterRegistry.All.Select(Id).Order().ToList(),
            FormerKeys.Keys.Order().ToList());
    }

    [Fact]
    public void Every_catalog_entry_has_the_key_it_was_published_with()
    {
        var mismatches = ParameterRegistry.All
            .Where(p => p.ToMqttKey() != FormerKeys[Id(p)])
            .Select(p => $"{Id(p)}: {p.ToMqttKey()} != {FormerKeys[Id(p)]}")
            .ToList();

        Assert.Empty(mismatches);
    }

    [Fact]
    public void Keys_are_unique_per_granularity()
    {
        var duplicates = ParameterRegistry.All
            .GroupBy(p => (p.Granularity, Key: p.ToMqttKey()))
            .Where(g => g.Count() > 1)
            .Select(g => g.Key.Key)
            .ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void A_parameter_outside_the_catalog_has_no_key()
    {
        var stranger = new ParameterDef("not_in_catalog", "", ParameterGroup.Weather, ParameterGranularity.Hourly);

        var ex = Assert.Throws<InvalidOperationException>(() => stranger.ToMqttKey());

        Assert.Equal("No MQTT key is defined for parameter 'not_in_catalog' (Hourly)", ex.Message);
    }
}
