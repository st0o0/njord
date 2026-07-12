using Njord.Domain;
using Njord.Egress;

namespace Njord.Tests.Egress;

public sealed class TopicSchemeSpec
{
    private static readonly WeatherModel IconD2 = new("icon_d2");
    private static readonly ParameterDef ApparentTemp = ParameterRegistry.GetByApiName("apparent_temperature")!;
    private static readonly ParameterDef TempMax = ParameterRegistry.GetByApiName("temperature_2m_max")!;

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
    public void Horizon_topic_carries_location_model_and_horizon()
    {
        Assert.Equal("njord/home/icon_d2/h3", TopicScheme.HorizonTopic("njord", "home", IconD2, "h3"));
        Assert.Equal("njord/home/icon_d2/d0", TopicScheme.HorizonTopic("njord", "home", IconD2, "d0"));
    }


    [Fact(Timeout = 5000)]
    public void Availability_topic_sits_at_the_base()
    {
        Assert.Equal("njord/status", TopicScheme.AvailabilityTopic("njord"));
    }

    [Fact(Timeout = 5000)]
    public void Hourly_unique_id_identifies_the_full_grid_coordinate()
    {
        Assert.Equal(
            "njord_home_icon_d2_apparent_temperature_h24",
            TopicScheme.HourlyUniqueId("home", IconD2, ApparentTemp, 24));
    }

    [Fact(Timeout = 5000)]
    public void Daily_unique_id_uses_day_offset()
    {
        Assert.Equal(
            "njord_home_icon_d2_temperature_max_d1",
            TopicScheme.DailyUniqueId("home", IconD2, TempMax, 1));
    }
}
