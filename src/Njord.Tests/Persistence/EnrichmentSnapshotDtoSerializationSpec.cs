using Newtonsoft.Json;
using Njord.Domain.Analysis;
using Njord.Persistence;

using static VerifyXunit.Verifier;

namespace Njord.Tests.Persistence;

public sealed class EnrichmentSnapshotDtoSerializationSpec
{
    [Fact(Timeout = 5000)]
    public Task EnrichmentSnapshot_dto_produces_stable_wire_format()
    {
        var state = new Dictionary<string, object>
        {
            ["lucerne|alerts"] = new AlertResult("lucerne", []),
            ["lucerne|indices"] = new IndexResult("lucerne", 80, 90, 70, 85, 95, 60, 12.5, 0.5, 88, 75, null, null),
        };
        var dto = EnrichmentSnapshotMapping.ToDto(state);
        var json = JsonConvert.SerializeObject(dto, Formatting.Indented);
        return Verify(json);
    }

    [Fact(Timeout = 5000)]
    public void EnrichmentSnapshot_dto_round_trips_to_domain()
    {
        var state = new Dictionary<string, object>
        {
            ["lucerne|alerts"] = new AlertResult("lucerne", []),
        };
        var dto = EnrichmentSnapshotMapping.ToDto(state);
        var json = JsonConvert.SerializeObject(dto);
        var deserialized = JsonConvert.DeserializeObject<EnrichmentSnapshotDto>(json)!;
        var result = EnrichmentSnapshotMapping.ToDomain(deserialized);

        Assert.Single(result);
        Assert.IsType<AlertResult>(result["lucerne|alerts"]);
    }
}
