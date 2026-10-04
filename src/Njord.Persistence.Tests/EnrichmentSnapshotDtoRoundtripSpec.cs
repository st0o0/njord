using Njord.Persistence;
using Njord.Tests.Shared;

namespace Njord.Persistence.Tests;

public sealed class EnrichmentSnapshotDtoRoundtripSpec
{
    [Fact]
    public void EnrichmentSnapshotDto_roundtrip()
    {
        var dto = new EnrichmentSnapshotDto
        {
            Version = 1,
            Enrichments = new Dictionary<string, EnrichmentEntryDto>
            {
                ["lucerne"] = new()
                {
                    TypeName = "AlertResult",
                    JsonPayload = "{\"alerts\":[]}",
                },
                ["zurich"] = new()
                {
                    TypeName = "ConsensusResult",
                    JsonPayload = "{\"consensus\":{}}",
                },
            },
        };

        var result = PersistenceRoundtripHelper.Roundtrip(dto);

        Assert.Equal(dto.Version, result.Version);
        Assert.Equal(2, result.Enrichments.Count);

        var lucerne = result.Enrichments["lucerne"];
        Assert.Equal("AlertResult", lucerne.TypeName);
        Assert.Equal("{\"alerts\":[]}", lucerne.JsonPayload);

        var zurich = result.Enrichments["zurich"];
        Assert.Equal("ConsensusResult", zurich.TypeName);
        Assert.Equal("{\"consensus\":{}}", zurich.JsonPayload);
    }
}
