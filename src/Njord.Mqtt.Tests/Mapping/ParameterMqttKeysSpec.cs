using Njord.Core.Configuration;
using Njord.Mqtt.Mapping;

namespace Njord.Mqtt.Tests.Mapping;

public sealed class ParameterMqttKeysSpec
{
    [Fact]
    public void Every_catalog_parameter_has_a_key()
    {
        var missing = ParameterRegistry.All
            .Where(p => !HasKey(p))
            .Select(p => $"{p.ApiName} ({p.Granularity})")
            .ToList();

        Assert.Empty(missing);
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
    public void Daily_variants_of_a_shared_api_name_get_distinct_keys()
    {
        Assert.Multiple(
            () => Assert.Equal("weather_code", ParameterRegistry.WeatherCode.ToMqttKey()),
            () => Assert.Equal("weather_code_daily", ParameterRegistry.DailyWeatherCode.ToMqttKey()));
    }

    [Fact]
    public void A_parameter_outside_the_catalog_has_no_key()
    {
        var stranger = new ParameterDef("not_in_catalog", "", ParameterGroup.Weather, ParameterGranularity.Hourly);

        var ex = Assert.Throws<InvalidOperationException>(() => stranger.ToMqttKey());

        Assert.Equal("No MQTT key is defined for parameter 'not_in_catalog' (Hourly)", ex.Message);
    }

    private static bool HasKey(ParameterDef parameter)
    {
        try
        {
            return parameter.ToMqttKey().Length > 0;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
