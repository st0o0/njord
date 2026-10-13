using Njord.Compute.Consensus;
using Njord.Compute.Contracts;

namespace Njord.Compute.Tests.Contracts;

public sealed class CategoricalValueSpec
{
    [Theory]
    [InlineData(TrendDirection.Rising, "rising")]
    [InlineData(TrendDirection.Falling, "falling")]
    [InlineData(TrendDirection.Stable, "stable")]
    public void Trend_direction_wire_name_is_lowercase(TrendDirection direction, string expected) =>
        Assert.Equal(expected, direction.ToWireName());

    [Theory]
    [InlineData(Stability.Converging, "converging")]
    [InlineData(Stability.Diverging, "diverging")]
    [InlineData(Stability.Stable, "stable")]
    public void Stability_wire_name_is_lowercase(Stability stability, string expected) =>
        Assert.Equal(expected, stability.ToWireName());

    [Theory]
    [InlineData(WeatherCategory.Clear, "clear")]
    [InlineData(WeatherCategory.Fog, "fog")]
    [InlineData(WeatherCategory.Drizzle, "drizzle")]
    [InlineData(WeatherCategory.Rain, "rain")]
    [InlineData(WeatherCategory.Snow, "snow")]
    [InlineData(WeatherCategory.Showers, "showers")]
    [InlineData(WeatherCategory.Thunderstorm, "thunderstorm")]
    [InlineData(WeatherCategory.Unknown, "unknown")]
    public void Weather_category_wire_name_is_lowercase(WeatherCategory category, string expected) =>
        Assert.Equal(expected, category.ToWireName());

    [Fact]
    public void Weather_change_description_joins_wire_names_with_an_arrow()
    {
        var change = new WeatherChangeResult(WeatherCategory.Clear, WeatherCategory.Rain);

        Assert.Equal("clear → rain", change.ToDescription());
    }

    [Theory]
    [InlineData(DewPointComfort.Dry, "dry")]
    [InlineData(DewPointComfort.Comfortable, "comfortable")]
    [InlineData(DewPointComfort.Sticky, "sticky")]
    [InlineData(DewPointComfort.Oppressive, "oppressive")]
    [InlineData(DewPointComfort.Dangerous, "dangerous")]
    public void Dew_point_comfort_wire_name_is_lowercase(DewPointComfort comfort, string expected) =>
        Assert.Equal(expected, comfort.ToWireName());

    [Theory]
    [InlineData(0, "Clear sky")]
    [InlineData(1, "Mainly clear")]
    [InlineData(2, "Partly cloudy")]
    [InlineData(3, "Overcast")]
    [InlineData(45, "Fog")]
    [InlineData(48, "Depositing rime fog")]
    [InlineData(51, "Drizzle: light")]
    [InlineData(53, "Drizzle: moderate")]
    [InlineData(55, "Drizzle: dense")]
    [InlineData(56, "Freezing drizzle: light")]
    [InlineData(57, "Freezing drizzle: dense")]
    [InlineData(61, "Rain: slight")]
    [InlineData(63, "Rain: moderate")]
    [InlineData(65, "Rain: heavy")]
    [InlineData(66, "Freezing rain: light")]
    [InlineData(67, "Freezing rain: heavy")]
    [InlineData(71, "Snow fall: slight")]
    [InlineData(73, "Snow fall: moderate")]
    [InlineData(75, "Snow fall: heavy")]
    [InlineData(77, "Snow grains")]
    [InlineData(80, "Rain showers: slight")]
    [InlineData(81, "Rain showers: moderate")]
    [InlineData(82, "Rain showers: violent")]
    [InlineData(85, "Snow showers: slight")]
    [InlineData(86, "Snow showers: heavy")]
    [InlineData(95, "Thunderstorm: slight or moderate")]
    [InlineData(96, "Thunderstorm with slight hail")]
    [InlineData(99, "Thunderstorm with heavy hail")]
    public void Wmo_code_maps_to_condition_with_unchanged_description(int code, string expected)
    {
        var condition = WeatherConditions.FromWmoCode(code);

        Assert.NotNull(condition);
        Assert.Equal(expected, condition.Value.ToDescription());
    }

    [Theory]
    [InlineData(4)]
    [InlineData(150)]
    [InlineData(-1)]
    public void Unknown_wmo_code_has_no_condition(int code) =>
        Assert.Null(WeatherConditions.FromWmoCode(code));

    [Fact]
    public void Missing_wmo_code_has_no_condition() =>
        Assert.Null(WeatherConditions.FromWmoCode(null));
}
