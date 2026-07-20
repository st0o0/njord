using Newtonsoft.Json;
using Njord.Pipeline;

namespace Njord.Persistence;

public sealed class DataChangedDto
{
    [JsonProperty("v")] public int Version { get; set; } = 1;
    [JsonProperty("loc")] public string Location { get; set; } = "";
    [JsonProperty("model")] public string ModelId { get; set; } = "";
    [JsonProperty("hash")] public int Hash { get; set; }
    [JsonProperty("utc")] public long UtcTicks { get; set; }
}

public static class SchedulerDtoMapping
{
    public static DataChangedDto ToDto(SchedulerActor.DataChanged evt) => new()
    {
        Location = evt.Location,
        ModelId = evt.ModelId,
        Hash = evt.Hash,
        UtcTicks = evt.Utc.UtcTicks,
    };

    public static SchedulerActor.DataChanged ToDomain(DataChangedDto dto) =>
        new(dto.Location, dto.ModelId, dto.Hash, new DateTimeOffset(dto.UtcTicks, TimeSpan.Zero));
}
