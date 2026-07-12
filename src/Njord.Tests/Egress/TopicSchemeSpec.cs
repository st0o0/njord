using Njord.Domain;
using Njord.Egress;

namespace Njord.Tests.Egress;

public sealed class TopicSchemeSpec
{
    private static readonly WeatherModel IconD2 = new("icon_d2");

    [Fact(Timeout = 5000)]
    public void Device_ids_combine_prefix_location_and_model()
    {
        Assert.Equal("njord_home_icon_d2", TopicScheme.DeviceId("home", IconD2));
    }

    [Fact(Timeout = 5000)]
    public void Location_names_are_slugified()
    {
        Assert.Equal("njord_z_rich_s_d_icon_d2", TopicScheme.DeviceId("Zürich Süd", IconD2));
    }

    [Fact(Timeout = 5000)]
    public void Config_topic_uses_device_based_discovery()
    {
        Assert.Equal(
            "homeassistant/device/njord_home_icon_d2/config",
            TopicScheme.ConfigTopic("homeassistant", "njord_home_icon_d2"));
    }

    [Fact(Timeout = 5000)]
    public void State_topic_carries_location_and_model()
    {
        Assert.Equal("njord/home/icon_d2/state", TopicScheme.StateTopic("njord", "home", IconD2));
    }

    [Fact(Timeout = 5000)]
    public void Availability_topic_sits_at_the_base()
    {
        Assert.Equal("njord/status", TopicScheme.AvailabilityTopic("njord"));
    }

    [Fact(Timeout = 5000)]
    public void Unique_ids_identify_the_full_grid_coordinate()
    {
        Assert.Equal(
            "njord_home_icon_d2_apparent_temperature_h24",
            TopicScheme.UniqueId("home", IconD2, WeatherParameter.ApparentTemperature, 24));
    }

    [Fact(Timeout = 5000)]
    public void Every_parameter_has_a_snake_case_json_key()
    {
        var keys = Enum.GetValues<WeatherParameter>().Select(p => p.JsonKey()).ToList();

        Assert.Equal(
        [
            "temperature", "apparent_temperature", "precipitation", "wind_speed",
            "wind_gust", "dewpoint", "relative_humidity", "cloud_cover", "pressure_msl",
        ], keys);
    }
}
