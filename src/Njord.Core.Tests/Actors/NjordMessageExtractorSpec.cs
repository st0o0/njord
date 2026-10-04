using Njord.Actors;
using Njord.Messages;

namespace Njord.Core.Tests.Actors;

public sealed class NjordMessageExtractorSpec
{
    private readonly NjordMessageExtractor _extractor = new();

    private sealed record ModelKeyMessage(string Location, string ModelId) : IWithModelKey;
    private sealed record EnrichmentKeyMessage(string Location, string TypeName) : IWithEnrichmentKey;
    private sealed record LocationMessage(string Location) : IWithLocation;

    [Fact]
    public void Extracts_entity_id_for_model_key_message()
    {
        var id = _extractor.EntityId(new ModelKeyMessage("borken", "icon_d2"));
        Assert.Equal("borken|icon_d2", id);
    }

    [Fact]
    public void Extracts_entity_id_for_enrichment_key_message()
    {
        var id = _extractor.EntityId(new EnrichmentKeyMessage("borken", "consensus"));
        Assert.Equal("borken|consensus", id);
    }

    [Fact]
    public void Extracts_entity_id_for_location_message()
    {
        var id = _extractor.EntityId(new LocationMessage("borken"));
        Assert.Equal("borken", id);
    }

    [Fact]
    public void Model_key_takes_precedence_over_location()
    {
        var msg = new ModelKeyMessage("borken", "icon_d2");
        var id = _extractor.EntityId(msg);
        Assert.Contains("|", id);
    }

    [Fact]
    public void Throws_for_unknown_message_type()
    {
        Assert.Throws<ArgumentException>(() => _extractor.EntityId("not a sharded message"));
    }
}
