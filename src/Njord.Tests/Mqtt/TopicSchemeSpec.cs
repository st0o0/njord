using Njord.Domain.Weather;
using Njord.Mqtt;

namespace Njord.Tests.Mqtt;

public sealed class TopicSchemeSpec
{
    private static readonly WeatherModel IconD2 = new("icon_d2");
    private static readonly ParameterDef ApparentTemp = ParameterRegistry.GetByApiName("apparent_temperature")!;
    private static readonly ParameterDef TempMax = ParameterRegistry.GetByApiName("temperature_2m_max")!;

    [Fact]
    public void Device_ids_combine_prefix_location_and_model()
    {
        Assert.Equal("njord_home_icon_d2", TopicScheme.DeviceId("home", IconD2));
    }

    [Fact]
    public void Location_names_are_slugified()
    {
        Assert.Equal("njord_z_rich_s_d_icon_d2", TopicScheme.DeviceId("Zürich Süd", IconD2));
    }

    [Fact]
    public void Config_topic_uses_device_based_discovery()
    {
        Assert.Equal(
            "homeassistant/device/njord_home_icon_d2/config",
            TopicScheme.ConfigTopic("homeassistant", "njord_home_icon_d2"));
    }

    [Fact]
    public void Horizon_topic_carries_location_model_and_horizon()
    {
        Assert.Equal("njord/home/icon_d2/h3", TopicScheme.HorizonTopic("njord", "home", IconD2, "h3"));
        Assert.Equal("njord/home/icon_d2/d0", TopicScheme.HorizonTopic("njord", "home", IconD2, "d0"));
    }


    [Fact]
    public void Availability_topic_sits_at_the_base()
    {
        Assert.Equal("njord/status", TopicScheme.AvailabilityTopic("njord"));
    }

    [Fact]
    public void Hourly_unique_id_identifies_the_full_grid_coordinate()
    {
        Assert.Equal(
            "njord_home_icon_d2_apparent_temperature_h24",
            TopicScheme.HourlyUniqueId("home", IconD2, ApparentTemp, 24));
    }

    [Fact]
    public void Daily_unique_id_uses_day_offset()
    {
        Assert.Equal(
            "njord_home_icon_d2_temperature_max_d1",
            TopicScheme.DailyUniqueId("home", IconD2, TempMax, 1));
    }

    [Fact]
    public void Consensus_device_id_uses_location_slug()
    {
        Assert.Equal("njord_lucerne_consensus", TopicScheme.EnrichmentDeviceId("lucerne", "consensus"));
    }

    [Fact]
    public void Consensus_horizon_topic_uses_consensus_segment()
    {
        Assert.Equal("njord/lucerne/consensus/h3",
            TopicScheme.EnrichmentSubTopic("njord", "lucerne", "consensus", "h3"));
    }

    [Fact]
    public void Alert_device_id_uses_location_slug()
    {
        Assert.Equal("njord_lucerne_alerts", TopicScheme.EnrichmentDeviceId("lucerne", "alerts"));
    }

    [Fact]
    public void Alert_topic_uses_alerts_segment()
    {
        Assert.Equal("njord/lucerne/alerts/frost",
            TopicScheme.EnrichmentSubTopic("njord", "lucerne", "alerts", "frost"));
        Assert.Equal("njord/lucerne/alerts/heavy-rain",
            TopicScheme.EnrichmentSubTopic("njord", "lucerne", "alerts", "heavy-rain"));
    }

    [Fact]
    public void Enrichment_device_id_combines_location_and_type_name()
    {
        Assert.Equal("njord_lucerne_consensus", TopicScheme.EnrichmentDeviceId("lucerne", "consensus"));
        Assert.Equal("njord_z_rich_alerts", TopicScheme.EnrichmentDeviceId("Zürich", "alerts"));
    }

    [Fact]
    public void Enrichment_topic_combines_base_location_and_type_name()
    {
        Assert.Equal("njord/lucerne/consensus", TopicScheme.EnrichmentTopic("njord", "lucerne", "consensus"));
        Assert.Equal("njord/home/energy", TopicScheme.EnrichmentTopic("njord", "home", "energy"));
    }

    [Fact]
    public void Enrichment_sub_topic_adds_sub_segment()
    {
        Assert.Equal("njord/lucerne/consensus/h3", TopicScheme.EnrichmentSubTopic("njord", "lucerne", "consensus", "h3"));
        Assert.Equal("njord/home/derived/meta", TopicScheme.EnrichmentSubTopic("njord", "home", "derived", "meta"));
    }
}
