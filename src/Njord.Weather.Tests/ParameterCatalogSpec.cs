namespace Njord.Weather.Tests;

public sealed class ParameterCatalogSpec
{
    [Fact]
    public void Catalog_contains_every_known_forecast_variable()
    {
        Assert.Equal(73, ParameterRegistry.All.Count);
    }

    [Fact]
    public void No_two_entries_share_api_name_and_granularity()
    {
        var duplicates = ParameterRegistry.All
            .GroupBy(p => (p.ApiName, p.Granularity))
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key.ApiName}/{g.Key.Granularity}")
            .ToList();

        Assert.Empty(duplicates);
    }

    [Theory]
    [InlineData(ParameterGroup.Weather, ParameterGranularity.Hourly, 31)]
    [InlineData(ParameterGroup.Weather, ParameterGranularity.Daily, 17)]
    [InlineData(ParameterGroup.Solar, ParameterGranularity.Hourly, 9)]
    [InlineData(ParameterGroup.Solar, ParameterGranularity.Daily, 4)]
    [InlineData(ParameterGroup.Soil, ParameterGranularity.Hourly, 11)]
    [InlineData(ParameterGroup.Soil, ParameterGranularity.Daily, 1)]
    public void Each_group_has_the_expected_hourly_and_daily_split(ParameterGroup group, ParameterGranularity granularity, int expected)
    {
        var count = ParameterRegistry.GetByGroup(group).Count(p => p.Granularity == granularity);

        Assert.Equal(expected, count);
    }

    [Fact]
    public void Weather_code_exists_in_both_granularities_as_distinct_entries()
    {
        var entries = ParameterRegistry.All.Where(p => p.ApiName == "weather_code").ToList();

        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, p => p.Granularity == ParameterGranularity.Hourly);
        Assert.Contains(entries, p => p.Granularity == ParameterGranularity.Daily);
        Assert.NotEqual(ParameterRegistry.WeatherCode, ParameterRegistry.DailyWeatherCode);
        Assert.Equal(ParameterGranularity.Hourly, ParameterRegistry.WeatherCode.Granularity);
        Assert.Equal(ParameterGranularity.Daily, ParameterRegistry.DailyWeatherCode.Granularity);
    }

    [Theory]
    [InlineData("sunshine_duration")]
    [InlineData("et0_fao_evapotranspiration")]
    public void Shared_api_names_have_a_hourly_and_a_daily_entry(string apiName)
    {
        var granularities = ParameterRegistry.All.Where(p => p.ApiName == apiName).Select(p => p.Granularity).Order().ToList();

        Assert.Equal([ParameterGranularity.Hourly, ParameterGranularity.Daily], granularities);
    }

    [Fact]
    public void Every_typed_field_is_a_catalog_entry_and_every_entry_has_exactly_one_field()
    {
        var fields = typeof(ParameterRegistry)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.FieldType == typeof(ParameterDef))
            .Select(f => (ParameterDef)f.GetValue(null)!)
            .ToList();

        Assert.Multiple(
            () => Assert.Equal(ParameterRegistry.All.Count, fields.Count),
            () => Assert.All(fields, p => Assert.Contains(p, ParameterRegistry.All)),
            () => Assert.Equal(fields.Count, fields.Distinct().Count()));
    }

    [Fact]
    public void Every_entry_is_reachable_by_group_and_by_api_name()
    {
        Assert.All(ParameterRegistry.All, p =>
        {
            Assert.Contains(p, ParameterRegistry.GetByGroup(p.Group));
            Assert.NotNull(ParameterRegistry.GetByApiName(p.ApiName));
        });
    }
}
