using Njord.Core.AirQuality;
using Njord.Core.Flood;
using Njord.Core.Forecast;
using Njord.Core.Marine;
using Njord.Core.Seasonal;
using Njord.OpenMeteo.AirQuality;
using Njord.OpenMeteo.Flood;
using Njord.OpenMeteo.Forecast;
using Njord.OpenMeteo.Marine;
using Njord.OpenMeteo.Seasonal;

namespace Njord.OpenMeteo.Tests.Mapping;

public sealed class ApiVariableMappingCompletenessTests
{
    [Fact]
    public void Should_map_every_weather_flag_to_a_distinct_named_api_variable()
    {
        var flags = Enum.GetValues<ForecastParams>().Where(f => f != ForecastParams.None).ToList();

        var variables = flags.Select(f => f.ToApiVariable()).ToList();

        Assert.Multiple(
            () => Assert.Equal(flags.Count, variables.Distinct().Count()),
            () => Assert.Equal(Enum.GetValues<ForecastApiVariable>().Length, variables.Count),
            () => Assert.Equal(variables.Count, ApiVariableNames.For<ForecastApiVariable>().Count),
            () => Assert.All(variables, v => Assert.False(string.IsNullOrEmpty(v.ToApiName()))));
    }

    [Fact]
    public void Should_map_every_air_quality_flag_to_a_distinct_named_api_variable()
    {
        var flags = Enum.GetValues<AirQualityParams>().Where(f => f != AirQualityParams.None).ToList();

        var variables = flags.Select(f => f.ToApiVariable()).ToList();

        Assert.Multiple(
            () => Assert.Equal(flags.Count, variables.Distinct().Count()),
            () => Assert.Equal(Enum.GetValues<AirQualityApiVariable>().Length, variables.Count),
            () => Assert.Equal(variables.Count, ApiVariableNames.For<AirQualityApiVariable>().Count),
            () => Assert.All(variables, v => Assert.False(string.IsNullOrEmpty(v.ToApiName()))));
    }

    [Fact]
    public void Should_map_every_flood_flag_to_a_distinct_named_api_variable()
    {
        var flags = Enum.GetValues<FloodParams>().Where(f => f != FloodParams.None && f != FloodParams.Ensemble).ToList();

        var variables = flags.Select(f => f.ToApiVariable()).ToList();

        Assert.Multiple(
            () => Assert.Equal(flags.Count, variables.Distinct().Count()),
            () => Assert.Equal(Enum.GetValues<FloodApiVariable>().Length, variables.Count),
            () => Assert.Equal(variables.Count, ApiVariableNames.For<FloodApiVariable>().Count),
            () => Assert.All(variables, v => Assert.False(string.IsNullOrEmpty(v.ToApiName()))));
    }

    [Fact]
    public void Should_map_every_marine_flag_to_a_distinct_named_api_variable()
    {
        var flags = Enum.GetValues<MarineParams>().Where(f => f != MarineParams.None).ToList();

        var variables = flags.Select(f => f.ToApiVariable()).ToList();

        Assert.Multiple(
            () => Assert.Equal(flags.Count, variables.Distinct().Count()),
            () => Assert.Equal(Enum.GetValues<MarineApiVariable>().Length, variables.Count),
            () => Assert.Equal(variables.Count, ApiVariableNames.For<MarineApiVariable>().Count),
            () => Assert.All(variables, v => Assert.False(string.IsNullOrEmpty(v.ToApiName()))));
    }

    [Fact]
    public void Should_map_every_seasonal_flag_to_a_distinct_named_api_variable()
    {
        var flags = Enum.GetValues<SeasonalParams>().Where(f => f != SeasonalParams.None).ToList();

        var variables = flags.Select(f => f.ToApiVariable()).ToList();

        Assert.Multiple(
            () => Assert.Equal(flags.Count, variables.Distinct().Count()),
            () => Assert.Equal(Enum.GetValues<SeasonalApiVariable>().Length, variables.Count),
            () => Assert.Equal(variables.Count, ApiVariableNames.For<SeasonalApiVariable>().Count),
            () => Assert.All(variables, v => Assert.False(string.IsNullOrEmpty(v.ToApiName()))));
    }

    [Fact]
    public void Should_chain_flag_to_variable_to_api_name()
    {
        Assert.Multiple(
            () => Assert.Equal(ForecastApiVariable.WindDirection, ForecastParams.WindDirection.ToApiVariable()),
            () => Assert.Equal("wind_direction_10m", ForecastApiVariable.WindDirection.ToApiName()),
            () => Assert.Equal(["pm2_5", "ozone"], (AirQualityParams.Pm25 | AirQualityParams.Ozone).ToApiNames()));
    }
}
