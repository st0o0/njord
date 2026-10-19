using Njord.Messages.Alerts;

namespace Njord.Messages.Tests.Contracts;

public sealed class AlertTypeExtensionsSpec
{
    public static TheoryData<AlertType, string> Segments => new()
    {
        { AlertType.Frost, "frost" },
        { AlertType.Heat, "heat" },
        { AlertType.Storm, "storm" },
        { AlertType.HeavyRain, "heavy-rain" },
        { AlertType.Uv, "uv" },
        { AlertType.Fog, "fog" },
        { AlertType.Snow, "snow" },
        { AlertType.PressureDrop, "pressure-drop" },
        { AlertType.Thunderstorm, "thunderstorm" },
        { AlertType.Ice, "ice" },
        { AlertType.WindChill, "wind-chill" },
        { AlertType.Visibility, "visibility" },
        { AlertType.TropicalNight, "tropical-night" },
        { AlertType.Humidity, "humidity" },
        { AlertType.Pollen, "pollen" },
        { AlertType.Pm25, "pm2_5" },
        { AlertType.FloodRisk, "flood_risk" },
        { AlertType.FloodRain, "flood_rain" },
    };

    [Theory]
    [MemberData(nameof(Segments))]
    public void ToTopicSegment_returns_the_documented_segment(AlertType type, string expected)
    {
        Assert.Equal(expected, type.ToTopicSegment());
    }

    [Fact]
    public void Segments_cover_every_AlertType_member()
    {
        var covered = Segments.Select(row => row.Data.Item1).Order().ToList();

        Assert.Equal(Enum.GetValues<AlertType>().Order().ToList(), covered);
    }

    [Fact]
    public void ToTopicSegment_throws_for_an_undefined_value()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => ((AlertType)999).ToTopicSegment());

        Assert.Equal("type", ex.ParamName);
        Assert.Equal((AlertType)999, ex.ActualValue);
    }

    [Theory]
    [InlineData(AlertType.Frost, true)]
    [InlineData(AlertType.Humidity, true)]
    [InlineData(AlertType.Pollen, false)]
    [InlineData(AlertType.Pm25, false)]
    [InlineData(AlertType.FloodRisk, false)]
    [InlineData(AlertType.FloodRain, false)]
    public void IsWeather_separates_weather_from_air_quality_and_flood(AlertType type, bool expected)
    {
        Assert.Equal(expected, type.IsWeather());
    }
}
