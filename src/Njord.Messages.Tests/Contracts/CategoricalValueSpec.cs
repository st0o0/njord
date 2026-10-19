using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.Messages.Tests.Contracts;

public sealed class CategoricalValueSpec
{
    [Theory]
    [InlineData(AlertSeverity.None, "none")]
    [InlineData(AlertSeverity.Yellow, "yellow")]
    [InlineData(AlertSeverity.Orange, "orange")]
    [InlineData(AlertSeverity.Red, "red")]
    public void Alert_severity_wire_name_is_lowercase(AlertSeverity severity, string expected) =>
        Assert.Equal(expected, severity.ToWireName());

    [Fact]
    public void Undefined_alert_severity_has_no_wire_name()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => ((AlertSeverity)99).ToWireName());

        Assert.Equal("severity", ex.ParamName);
    }

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
