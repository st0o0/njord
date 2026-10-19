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
    public void Domain_topic_carries_slugged_location_domain_and_parameter()
    {
        Assert.Equal("njord/z_rich/air-quality/pm25",
            TopicScheme.DomainTopic("njord", "Zürich", DomainKind.AirQuality, "pm25"));
    }

    [Fact]
    public void Availability_topic_sits_at_the_base()
    {
        Assert.Equal("njord/status", TopicScheme.AvailabilityTopic("njord"));
    }

    [Fact]
    public void Enrichment_topics_nest_location_type_and_leaf()
    {
        Assert.Multiple(
            () => Assert.Equal("njord/lucerne/consensus", TopicScheme.EnrichmentTopic("njord", "Lucerne", "consensus")),
            () => Assert.Equal("njord/lucerne/alerts/heavy-rain",
                TopicScheme.EnrichmentSubTopic("njord", "Lucerne", "alerts", "heavy-rain")),
            () => Assert.Equal("njord_z_rich_alerts", TopicScheme.EnrichmentDeviceId("Zürich", "alerts")));
    }

    [Theory]
    [InlineData("ICON_D2", "icon_d2")]
    [InlineData("My Location", "my_location")]
    [InlineData("icon-eu.2.5km", "icon_eu_2_5km")]
    public void Slug_lowercases_and_replaces_everything_but_letters_and_digits(string input, string expected)
    {
        Assert.Equal(expected, TopicSlug.Slug(input));
    }
}
