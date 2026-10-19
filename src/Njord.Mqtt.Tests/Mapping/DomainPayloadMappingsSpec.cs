using System.Collections.Immutable;
using System.Globalization;
using Njord.Model;
using Njord.Mqtt.Mapping;

namespace Njord.Mqtt.Tests.Mapping;

public sealed class DomainPayloadMappingsSpec
{
    private const string Stamps = "\"valid_from\":\"2026-01-15T13:00:00+00:00\",\"model_run\":\"2026-01-15T12:00:00+00:00\"";

    private static readonly DateTimeOffset Run = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Valid = Run.AddHours(1);
    private static readonly Location Lucerne = new("Lucerne", 47.05, 8.31);

    private static DomainForecast<T> Forecast<T>(DomainKind domain, params T[] points)
        where T : IHasValidTime
        => new(domain, Run, TimeResolution.Hourly, [.. points]);

    private static Dictionary<string, string> ByTopic(IEnumerable<MqttMessage> messages)
        => messages.ToDictionary(m => m.Topic, m => m.Payload);

    [Fact]
    public void Weather_publishes_the_state_payload_and_a_scalar_topic_per_present_value()
    {
        var point = new WeatherPoint(Valid, 10.5, 9, 2, 0, 0, 50, 1013, 3, 60, 5, 1.5, EnsembleStdDev: 0.5, IsDay: true);

        var topics = ByTopic(Forecast(DomainKind.Weather, point).ToMessages(Lucerne, "njord"));

        Assert.Multiple(
            () => Assert.Equal(14, topics.Count),
            () => Assert.Equal(
                "{" + Stamps + ",\"temperature\":10.5,\"apparent_temperature\":9,\"wind_speed\":2,\"precipitation\":0,"
                + "\"precipitation_probability\":0,\"cloud_cover\":50,\"pressure\":1013,\"weather_code\":3,\"humidity\":60,"
                + "\"dew_point\":5,\"uv_index\":1.5,\"ensemble_std_dev\":0.5,\"is_day\":true}",
                topics["njord/lucerne/weather/state"]),
            () => Assert.Equal("10.5", topics["njord/lucerne/weather/temperature"]),
            () => Assert.Equal("true", topics["njord/lucerne/weather/is_day"]),
            () => Assert.DoesNotContain("njord/lucerne/weather/wind_gusts", topics.Keys));
    }

    [Fact]
    public void Air_quality_publishes_state_and_scalar_topics()
    {
        var topics = ByTopic(Forecast(DomainKind.AirQuality, new AirQualityPoint(Valid, 8.5, 12, 60, BirchPollen: 3)).ToMessages(Lucerne, "njord"));

        Assert.Multiple(
            () => Assert.Equal(5, topics.Count),
            () => Assert.Equal(
                "{" + Stamps + ",\"pm25\":8.5,\"pm10\":12,\"ozone\":60,\"birch_pollen\":3}",
                topics["njord/lucerne/air-quality/state"]),
            () => Assert.Equal("3", topics["njord/lucerne/air-quality/birch_pollen"]));
    }

    [Fact]
    public void Flood_omits_absent_optional_values()
    {
        var topics = ByTopic(Forecast(DomainKind.Flood, new FloodPoint(Valid, 12.5)).ToMessages(Lucerne, "njord"));

        Assert.Multiple(
            () => Assert.Equal(2, topics.Count),
            () => Assert.Equal("{" + Stamps + ",\"discharge\":12.5}", topics["njord/lucerne/flood/state"]));
    }

    [Fact]
    public void Solar_publishes_state_and_scalar_topics()
    {
        var topics = ByTopic(Forecast(DomainKind.Solar, new SolarPoint(Valid, 100, 50, 150, 3600)).ToMessages(Lucerne, "njord"));

        Assert.Multiple(
            () => Assert.Equal(5, topics.Count),
            () => Assert.Equal(
                "{" + Stamps + ",\"direct_radiation\":100,\"diffuse_radiation\":50,\"shortwave_radiation\":150,\"sunshine_duration\":3600}",
                topics["njord/lucerne/solar/state"]));
    }

    [Fact]
    public void Marine_publishes_state_and_scalar_topics()
    {
        var topics = ByTopic(Forecast(DomainKind.Marine, new MarinePoint(Valid, 1.5, 6, 270, 14.5)).ToMessages(Lucerne, "njord"));

        Assert.Multiple(
            () => Assert.Equal(5, topics.Count),
            () => Assert.Equal(
                "{" + Stamps + ",\"wave_height\":1.5,\"wave_period\":6,\"wave_direction\":270,\"water_temperature\":14.5}",
                topics["njord/lucerne/marine/state"]));
    }

    [Fact]
    public void Only_the_first_point_of_the_forecast_is_published()
    {
        var first = new FloodPoint(Valid, 1);

        var topics = ByTopic(Forecast(DomainKind.Flood, first, first with { ValidFrom = Valid.AddHours(1), Discharge = 99 }).ToMessages(Lucerne, "njord"));

        Assert.Equal("1", topics["njord/lucerne/flood/discharge"]);
    }

    [Fact]
    public void Scalar_values_use_the_invariant_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            var topics = ByTopic(Forecast(DomainKind.Flood, new FloodPoint(Valid, 12.5)).ToMessages(Lucerne, "njord"));

            Assert.Equal("12.5", topics["njord/lucerne/flood/discharge"]);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void An_empty_forecast_publishes_nothing()
    {
        var empty = new DomainForecast<FloodPoint>(DomainKind.Flood, Run, TimeResolution.Hourly, ImmutableArray<FloodPoint>.Empty);

        Assert.Empty(empty.ToMessages(Lucerne, "njord"));
    }
}
