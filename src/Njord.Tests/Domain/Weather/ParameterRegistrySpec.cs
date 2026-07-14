using Njord.Domain.Weather;

namespace Njord.Tests.Domain.Weather;

public sealed class ParameterRegistrySpec
{
    [Fact(Timeout = 5000)]
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

    [Fact(Timeout = 5000)]
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

    [Fact(Timeout = 5000)]
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

    [Fact(Timeout = 5000)]
    public void Resolve_with_weather_group_returns_hourly_and_daily()
    {
        var resolved = ParameterRegistry.Resolve(["Weather"], [], []);

        Assert.True(resolved.Hourly.Count > 0);
        Assert.True(resolved.Daily.Count > 0);
        Assert.All(resolved.Hourly, p => Assert.Equal(ParameterGranularity.Hourly, p.Granularity));
        Assert.All(resolved.Daily, p => Assert.Equal(ParameterGranularity.Daily, p.Granularity));
    }

    [Fact(Timeout = 5000)]
    public void Resolve_extra_adds_parameters_from_other_groups()
    {
        var resolved = ParameterRegistry.Resolve(["Weather"], ["uv_index"], []);

        Assert.Contains(resolved.Hourly, p => p.ApiName == "uv_index");
    }

    [Fact(Timeout = 5000)]
    public void Resolve_exclude_removes_parameters()
    {
        var resolved = ParameterRegistry.Resolve(["Weather"], [], ["cape"]);

        Assert.DoesNotContain(resolved.Hourly, p => p.ApiName == "cape");
    }

    [Fact(Timeout = 5000)]
    public void Resolve_unknown_group_throws()
    {
        var ex = Assert.Throws<ParameterResolutionException>(
            () => ParameterRegistry.Resolve(["Bogus"], [], []));

        Assert.Contains("Unknown parameter group", ex.Message);
    }

    [Fact(Timeout = 5000)]
    public void Resolve_unknown_extra_throws()
    {
        var ex = Assert.Throws<ParameterResolutionException>(
            () => ParameterRegistry.Resolve(["Weather"], ["not_real"], []));

        Assert.Contains("Unknown parameter in Extra", ex.Message);
    }

    [Fact(Timeout = 5000)]
    public void Resolve_empty_result_throws()
    {
        var allWeatherHourly = ParameterRegistry.GetByGroup(ParameterGroup.Weather)
            .Select(p => p.ApiName).ToList();

        var ex = Assert.Throws<ParameterResolutionException>(
            () => ParameterRegistry.Resolve(["Weather"], [], allWeatherHourly));

        Assert.Contains("empty", ex.Message);
    }

    [Fact(Timeout = 5000)]
    public void Every_registry_entry_has_a_non_null_unit()
    {
        Assert.All(ParameterRegistry.All, p => Assert.NotNull(p.Unit));
    }

    [Fact(Timeout = 5000)]
    public void Api_call_weight_is_ceil_of_hourly_count_over_ten()
    {
        var resolved = ParameterRegistry.Resolve(["Weather"], [], []);

        Assert.Equal((int)Math.Ceiling(resolved.HourlyCount / 10.0), resolved.ApiCallWeight);
    }

    [Fact(Timeout = 5000)]
    public void Sunrise_and_sunset_are_time_string_parameters()
    {
        var sunrise = ParameterRegistry.GetByApiName("sunrise");
        var sunset = ParameterRegistry.GetByApiName("sunset");

        Assert.NotNull(sunrise);
        Assert.NotNull(sunset);
        Assert.Equal(ParameterValueType.TimeString, sunrise.ValueType);
        Assert.Equal(ParameterValueType.TimeString, sunset.ValueType);
    }
}
