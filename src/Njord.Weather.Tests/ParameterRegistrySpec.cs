using Njord.Core.Configuration;

namespace Njord.Weather.Tests;

public sealed class ParameterRegistrySpec
{
    [Fact]
    public void Weather_group_contains_core_hourly_variables()
    {
        var hourly = ParameterRegistry.GetByGroup(ParameterGroup.Weather)
            .Where(p => p.Granularity == ParameterGranularity.Hourly)
            .Select(p => p.ApiName)
            .ToHashSet();

        Assert.Contains("temperature_2m", hourly);
        Assert.Contains("wind_speed_10m", hourly);
        Assert.Contains("wind_gusts_10m", hourly);
        Assert.Contains("precipitation", hourly);
        Assert.Contains("cloud_cover", hourly);
        Assert.Contains("pressure_msl", hourly);
        Assert.Contains("wind_direction_10m", hourly);
        Assert.Contains("visibility", hourly);
        Assert.Contains("cape", hourly);
    }

    [Fact]
    public void Solar_group_contains_radiation_variables()
    {
        var solar = ParameterRegistry.GetByGroup(ParameterGroup.Solar)
            .Select(p => p.ApiName)
            .ToHashSet();

        Assert.Contains("shortwave_radiation", solar);
        Assert.Contains("uv_index", solar);
        Assert.Contains("direct_radiation", solar);
        Assert.Contains("uv_index_max", solar);
    }

    [Fact]
    public void Soil_group_contains_soil_variables()
    {
        var soil = ParameterRegistry.GetByGroup(ParameterGroup.Soil)
            .Select(p => p.ApiName)
            .ToHashSet();

        Assert.Contains("soil_temperature_0cm", soil);
        Assert.Contains("soil_moisture_0_to_1cm", soil);
        Assert.Contains("evapotranspiration", soil);
        Assert.Contains("et0_fao_evapotranspiration", soil);
    }

    [Fact]
    public void Every_registry_entry_has_a_non_null_unit()
    {
        Assert.All(ParameterRegistry.All, p => Assert.NotNull(p.Unit));
    }

    [Fact]
    public void Api_call_weight_is_ceil_of_hourly_count_over_ten()
    {
        var resolved = ParameterSelection.Resolve(["Weather"], [], []);

        Assert.Equal((int)Math.Ceiling(resolved.HourlyCount / 10.0), resolved.ApiCallWeight);
    }

    [Fact]
    public void Sunrise_and_sunset_are_time_string_parameters()
    {
        var sunrise = ParameterRegistry.GetByApiName("sunrise");
        var sunset = ParameterRegistry.GetByApiName("sunset");

        Assert.NotNull(sunrise);
        Assert.NotNull(sunset);
        Assert.Equal(ParameterValueType.TimeString, sunrise.ValueType);
        Assert.Equal(ParameterValueType.TimeString, sunset.ValueType);
    }

    [Fact]
    public void Hourly_typed_properties_resolve_correct_parameters()
    {
        Assert.Multiple(
            () => Assert.Equal("rain", ParameterRegistry.Rain.ApiName),
            () => Assert.Equal("soil_temperature_0cm", ParameterRegistry.SoilTemperature0cm.ApiName),
            () => Assert.Equal("visibility", ParameterRegistry.Visibility.ApiName),
            () => Assert.Equal("wind_direction_10m", ParameterRegistry.WindDirection10m.ApiName),
            () => Assert.Equal(ParameterGranularity.Hourly, ParameterRegistry.Rain.Granularity),
            () => Assert.Equal(ParameterGranularity.Hourly, ParameterRegistry.SoilTemperature0cm.Granularity),
            () => Assert.Equal(ParameterGranularity.Hourly, ParameterRegistry.Visibility.Granularity),
            () => Assert.Equal(ParameterGranularity.Hourly, ParameterRegistry.WindDirection10m.Granularity));
    }

    [Fact]
    public void Daily_typed_properties_resolve_correct_parameters()
    {
        Assert.Multiple(
            () => Assert.Equal("precipitation_sum", ParameterRegistry.PrecipitationSum.ApiName),
            () => Assert.Equal("uv_index_max", ParameterRegistry.UvIndexMax.ApiName),
            () => Assert.Equal("snowfall_sum", ParameterRegistry.SnowfallSum.ApiName),
            () => Assert.Equal("temperature_2m_max", ParameterRegistry.Temperature2mMax.ApiName),
            () => Assert.Equal("temperature_2m_min", ParameterRegistry.Temperature2mMin.ApiName),
            () => Assert.Equal("wind_speed_10m_max", ParameterRegistry.WindSpeed10mMax.ApiName),
            () => Assert.Equal("wind_gusts_10m_max", ParameterRegistry.WindGusts10mMax.ApiName),
            () => Assert.Equal("sunrise", ParameterRegistry.Sunrise.ApiName),
            () => Assert.Equal("sunset", ParameterRegistry.Sunset.ApiName),
            () => Assert.Equal("weather_code", ParameterRegistry.DailyWeatherCode.ApiName));
    }

    [Fact]
    public void Daily_typed_properties_are_daily_granularity()
    {
        var dailyParams = new[]
        {
            ParameterRegistry.PrecipitationSum, ParameterRegistry.UvIndexMax,
            ParameterRegistry.SnowfallSum, ParameterRegistry.Temperature2mMax,
            ParameterRegistry.Temperature2mMin, ParameterRegistry.WindSpeed10mMax,
            ParameterRegistry.WindGusts10mMax, ParameterRegistry.Sunrise,
            ParameterRegistry.Sunset, ParameterRegistry.DailyWeatherCode,
        };

        Assert.All(dailyParams, p => Assert.Equal(ParameterGranularity.Daily, p.Granularity));
    }

    [Fact]
    public void Sunrise_and_sunset_typed_properties_are_time_string_type()
    {
        Assert.Multiple(
            () => Assert.Equal(ParameterValueType.TimeString, ParameterRegistry.Sunrise.ValueType),
            () => Assert.Equal(ParameterValueType.TimeString, ParameterRegistry.Sunset.ValueType));
    }
}
