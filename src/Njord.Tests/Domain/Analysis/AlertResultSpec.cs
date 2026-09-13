using Njord.Domain.Analysis;

namespace Njord.Tests.Domain.Analysis;

public sealed class AlertResultSpec
{
    [Fact]
    public void Alert_none_has_zero_confidence_and_none_severity()
    {
        var alert = Alert.None(AlertType.Frost);
        Assert.Equal(AlertSeverity.None, alert.Severity);
        Assert.Equal(0.0, alert.Confidence);
        Assert.Empty(alert.Attributes);
    }

    [Fact]
    public void All_alert_type_topic_segments()
    {
        Assert.Equal("frost", AlertType.Frost.ToTopicSegment());
        Assert.Equal("heat", AlertType.Heat.ToTopicSegment());
        Assert.Equal("storm", AlertType.Storm.ToTopicSegment());
        Assert.Equal("heavy-rain", AlertType.HeavyRain.ToTopicSegment());
        Assert.Equal("uv", AlertType.Uv.ToTopicSegment());
        Assert.Equal("fog", AlertType.Fog.ToTopicSegment());
        Assert.Equal("snow", AlertType.Snow.ToTopicSegment());
        Assert.Equal("pressure-drop", AlertType.PressureDrop.ToTopicSegment());
        Assert.Equal("thunderstorm", AlertType.Thunderstorm.ToTopicSegment());
        Assert.Equal("ice", AlertType.Ice.ToTopicSegment());
        Assert.Equal("wind-chill", AlertType.WindChill.ToTopicSegment());
        Assert.Equal("visibility", AlertType.Visibility.ToTopicSegment());
        Assert.Equal("tropical-night", AlertType.TropicalNight.ToTopicSegment());
        Assert.Equal("humidity", AlertType.Humidity.ToTopicSegment());
    }

    [Fact]
    public void AlertType_has_14_values()
    {
        Assert.Equal(14, Enum.GetValues<AlertType>().Length);
    }
}
