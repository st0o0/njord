using Njord.Messages.Alerts;

namespace Njord.Messages.Tests.Alerts;

public sealed class AlertTypeSpec
{
    [Fact]
    public void Should_define_eighteen_alert_types()
    {
        Assert.Equal(18, Enum.GetValues<AlertType>().Length);
    }

    [Theory]
    [InlineData(AlertType.Frost, "frost")]
    [InlineData(AlertType.HeavyRain, "heavy-rain")]
    [InlineData(AlertType.PressureDrop, "pressure-drop")]
    [InlineData(AlertType.WindChill, "wind-chill")]
    [InlineData(AlertType.TropicalNight, "tropical-night")]
    [InlineData(AlertType.Pm25, "pm2_5")]
    [InlineData(AlertType.FloodRisk, "flood_risk")]
    [InlineData(AlertType.FloodRain, "flood_rain")]
    public void Should_map_alert_type_to_topic_segment(AlertType type, string expected)
    {
        Assert.Equal(expected, type.ToTopicSegment());
    }

    [Fact]
    public void Should_give_every_alert_type_a_unique_non_empty_topic_segment()
    {
        var segments = Enum.GetValues<AlertType>().Select(t => t.ToTopicSegment()).ToList();

        Assert.Multiple(
            () => Assert.All(segments, s => Assert.False(string.IsNullOrWhiteSpace(s))),
            () => Assert.Equal(segments.Count, segments.Distinct().Count()));
    }

    [Fact]
    public void Should_classify_only_air_quality_and_flood_types_as_non_weather()
    {
        var nonWeather = Enum.GetValues<AlertType>().Where(t => !t.IsWeather());

        Assert.Equal(
            [AlertType.Pollen, AlertType.Pm25, AlertType.FloodRisk, AlertType.FloodRain],
            nonWeather.Order());
    }

    [Fact]
    public void Should_throw_for_undefined_alert_type()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => ((AlertType)999).ToTopicSegment());

        Assert.Equal("type", ex.ParamName);
    }
}
