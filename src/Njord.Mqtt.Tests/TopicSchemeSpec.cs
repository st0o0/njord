using Njord.Model;
using Njord.Mqtt.Topics;

namespace Njord.Mqtt.Tests;

public sealed class TopicSchemeSpec
{

    [Theory]
    [InlineData(DomainKind.Weather, "weather")]
    [InlineData(DomainKind.AirQuality, "air-quality")]
    [InlineData(DomainKind.Flood, "flood")]
    [InlineData(DomainKind.Solar, "solar")]
    [InlineData(DomainKind.Marine, "marine")]
    public void Every_domain_has_its_topic_segment(DomainKind domain, string segment)
    {
        Assert.Equal(segment, domain.ToTopicSegment());
    }

    [Fact]
    public void An_unknown_domain_has_no_topic_segment()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => ((DomainKind)99).ToTopicSegment());

        Assert.Equal("domain", ex.ParamName);
    }

    [Fact]
    public void Domain_topic_carries_location_domain_and_parameter()
    {
        Assert.Multiple(
            () => Assert.Equal("njord/lucerne/weather/temperature",
                TopicScheme.DomainTopic("njord", "Lucerne", DomainKind.Weather, "temperature")),
            () => Assert.Equal("njord/lucerne/air-quality/pm25",
                TopicScheme.DomainTopic("njord", "Lucerne", DomainKind.AirQuality, "pm25")),
            () => Assert.Equal("njord/z_rich/flood/discharge",
                TopicScheme.DomainTopic("njord", "Zürich", DomainKind.Flood, "discharge")));
    }

    [Fact]
    public void Availability_topic_sits_at_the_base()
    {
        Assert.Equal("njord/status", TopicScheme.AvailabilityTopic("njord"));
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
