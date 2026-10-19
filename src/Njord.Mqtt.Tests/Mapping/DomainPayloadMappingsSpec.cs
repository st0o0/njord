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
    public void Weather_publishes_the_state_payload_and_one_scalar_topic_per_present_value()
    {
        var point = new WeatherPoint(Valid, 10.5, 9, 2, 0, 0, 50, 1013, 3, 60, 5, 1.5, EnsembleStdDev: 0.5, IsDay: true);

        var messages = Forecast(DomainKind.Weather, point).ToMessages(Lucerne, "njord");
        var topics = ByTopic(messages);

        Assert.Multiple(
            () => Assert.All(messages, m => Assert.True(m.Retain)),
            () => Assert.Equal(14, messages.Count),
            () => Assert.Equal(
                "{" + Stamps + ",\"temperature\":10.5,\"apparent_temperature\":9,\"wind_speed\":2,\"precipitation\":0,"
                + "\"precipitation_probability\":0,\"cloud_cover\":50,\"pressure\":1013,\"weather_code\":3,\"humidity\":60,"
                + "\"dew_point\":5,\"uv_index\":1.5,\"ensemble_std_dev\":0.5,\"is_day\":true}",
                topics["njord/lucerne/weather/state"]),
            () => Assert.Equal("10.5", topics["njord/lucerne/weather/temperature"]),
            () => Assert.Equal("3", topics["njord/lucerne/weather/weather_code"]),
            () => Assert.Equal("0.5", topics["njord/lucerne/weather/ensemble_std_dev"]),
            () => Assert.Equal("true", topics["njord/lucerne/weather/is_day"]),
            () => Assert.DoesNotContain("njord/lucerne/weather/wind_gusts", topics.Keys));
    }

    [Fact]
    public void Weather_includes_optional_values_when_present()
    {
        var point = new WeatherPoint(Valid, 10, 9, 2, 0, 0, 50, 1013, 0, 60, 5, 0,
            WindGusts: 8.5, Visibility: 9000, Cape: 12, Rain: 0.2, Snowfall: 0.1, SoilTemperature: 4, FreezingLevelHeight: 1500, IsDay: false);

        var topics = ByTopic(Forecast(DomainKind.Weather, point).ToMessages(Lucerne, "njord"));

        Assert.Multiple(
            () => Assert.Equal(20, topics.Count),
            () => Assert.Equal("8.5", topics["njord/lucerne/weather/wind_gusts"]),
            () => Assert.Equal("9000", topics["njord/lucerne/weather/visibility"]),
            () => Assert.Equal("1500", topics["njord/lucerne/weather/freezing_level_height"]),
            () => Assert.Equal("false", topics["njord/lucerne/weather/is_day"]),
            () => Assert.EndsWith(
                "\"wind_gusts\":8.5,\"visibility\":9000,\"cape\":12,\"rain\":0.2,\"snowfall\":0.1,"
                + "\"soil_temperature\":4,\"freezing_level_height\":1500,\"is_day\":false}",
                topics["njord/lucerne/weather/state"]));
    }

    [Fact]
    public void Weather_publishes_the_first_point_of_the_forecast()
    {
        var first = new WeatherPoint(Valid, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);
        var second = first with { ValidFrom = Valid.AddHours(1), Temperature = 99 };

        var topics = ByTopic(Forecast(DomainKind.Weather, first, second).ToMessages(Lucerne, "njord"));

        Assert.Equal("1", topics["njord/lucerne/weather/temperature"]);
    }

    [Fact]
    public void Air_quality_publishes_the_state_payload_and_its_scalar_topics()
    {
        var point = new AirQualityPoint(Valid, 8.5, 12, 60, BirchPollen: 3);

        var topics = ByTopic(Forecast(DomainKind.AirQuality, point).ToMessages(Lucerne, "njord"));

        Assert.Multiple(
            () => Assert.Equal(5, topics.Count),
            () => Assert.Equal(
                "{" + Stamps + ",\"pm25\":8.5,\"pm10\":12,\"ozone\":60,\"birch_pollen\":3}",
                topics["njord/lucerne/air-quality/state"]),
            () => Assert.Equal("8.5", topics["njord/lucerne/air-quality/pm25"]),
            () => Assert.Equal("12", topics["njord/lucerne/air-quality/pm10"]),
            () => Assert.Equal("60", topics["njord/lucerne/air-quality/ozone"]),
            () => Assert.Equal("3", topics["njord/lucerne/air-quality/birch_pollen"]));
    }

    [Fact]
    public void Air_quality_includes_every_pollen_value_when_present()
    {
        var point = new AirQualityPoint(Valid, 1, 2, 3, 4, 5, 6, 7);

        var topics = ByTopic(Forecast(DomainKind.AirQuality, point).ToMessages(Lucerne, "njord"));

        Assert.Multiple(
            () => Assert.Equal(8, topics.Count),
            () => Assert.Equal("5", topics["njord/lucerne/air-quality/grass_pollen"]),
            () => Assert.Equal("6", topics["njord/lucerne/air-quality/alder"]),
            () => Assert.Equal("7", topics["njord/lucerne/air-quality/mugwort"]));
    }

    [Fact]
    public void Flood_publishes_the_state_payload_and_its_scalar_topics()
    {
        var point = new FloodPoint(Valid, 12.5, DischargeP10: 10, DischargeP90: 20, MedianHistorical: 11);

        var topics = ByTopic(Forecast(DomainKind.Flood, point).ToMessages(Lucerne, "njord"));

        Assert.Multiple(
            () => Assert.Equal(5, topics.Count),
            () => Assert.Equal(
                "{" + Stamps + ",\"discharge\":12.5,\"discharge_p10\":10,\"discharge_p90\":20,\"median_historical\":11}",
                topics["njord/lucerne/flood/state"]),
            () => Assert.Equal("12.5", topics["njord/lucerne/flood/discharge"]),
            () => Assert.Equal("10", topics["njord/lucerne/flood/discharge_p10"]),
            () => Assert.Equal("20", topics["njord/lucerne/flood/discharge_p90"]),
            () => Assert.Equal("11", topics["njord/lucerne/flood/median_historical"]));
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
    public void Solar_publishes_the_state_payload_and_its_scalar_topics()
    {
        var topics = ByTopic(Forecast(DomainKind.Solar, new SolarPoint(Valid, 100, 50, 150, 3600)).ToMessages(Lucerne, "njord"));

        Assert.Multiple(
            () => Assert.Equal(5, topics.Count),
            () => Assert.Equal(
                "{" + Stamps + ",\"direct_radiation\":100,\"diffuse_radiation\":50,\"shortwave_radiation\":150,\"sunshine_duration\":3600}",
                topics["njord/lucerne/solar/state"]),
            () => Assert.Equal("100", topics["njord/lucerne/solar/direct_radiation"]),
            () => Assert.Equal("50", topics["njord/lucerne/solar/diffuse_radiation"]),
            () => Assert.Equal("150", topics["njord/lucerne/solar/shortwave_radiation"]),
            () => Assert.Equal("3600", topics["njord/lucerne/solar/sunshine_duration"]));
    }

    [Fact]
    public void Marine_publishes_the_state_payload_and_its_scalar_topics()
    {
        var topics = ByTopic(Forecast(DomainKind.Marine, new MarinePoint(Valid, 1.5, 6, 270, 14.5)).ToMessages(Lucerne, "njord"));

        Assert.Multiple(
            () => Assert.Equal(5, topics.Count),
            () => Assert.Equal(
                "{" + Stamps + ",\"wave_height\":1.5,\"wave_period\":6,\"wave_direction\":270,\"water_temperature\":14.5}",
                topics["njord/lucerne/marine/state"]),
            () => Assert.Equal("1.5", topics["njord/lucerne/marine/wave_height"]),
            () => Assert.Equal("14.5", topics["njord/lucerne/marine/water_temperature"]));
    }

    [Fact]
    public void Marine_omits_the_water_temperature_when_absent()
    {
        var topics = ByTopic(Forecast(DomainKind.Marine, new MarinePoint(Valid, 1.5, 6, 270)).ToMessages(Lucerne, "njord"));

        Assert.Multiple(
            () => Assert.Equal(4, topics.Count),
            () => Assert.DoesNotContain("njord/lucerne/marine/water_temperature", topics.Keys),
            () => Assert.DoesNotContain("water_temperature", topics["njord/lucerne/marine/state"], StringComparison.Ordinal));
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
    public void Empty_forecasts_publish_nothing()
    {
        Assert.Multiple(
            () => Assert.Empty(new DomainForecast<WeatherPoint>(DomainKind.Weather, Run, TimeResolution.Hourly, ImmutableArray<WeatherPoint>.Empty).ToMessages(Lucerne, "njord")),
            () => Assert.Empty(new DomainForecast<AirQualityPoint>(DomainKind.AirQuality, Run, TimeResolution.Hourly, ImmutableArray<AirQualityPoint>.Empty).ToMessages(Lucerne, "njord")),
            () => Assert.Empty(new DomainForecast<FloodPoint>(DomainKind.Flood, Run, TimeResolution.Hourly, ImmutableArray<FloodPoint>.Empty).ToMessages(Lucerne, "njord")),
            () => Assert.Empty(new DomainForecast<SolarPoint>(DomainKind.Solar, Run, TimeResolution.Hourly, ImmutableArray<SolarPoint>.Empty).ToMessages(Lucerne, "njord")),
            () => Assert.Empty(new DomainForecast<MarinePoint>(DomainKind.Marine, Run, TimeResolution.Hourly, ImmutableArray<MarinePoint>.Empty).ToMessages(Lucerne, "njord")));
    }
}
