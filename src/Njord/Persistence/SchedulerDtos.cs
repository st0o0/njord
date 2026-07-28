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

public sealed class ModelPollStateDto
{
    [JsonProperty("hash")] public int? LastHash { get; set; }
    [JsonProperty("lastChange")] public long? LastChangeUtcTicks { get; set; }
    [JsonProperty("prevChange")] public long? PrevChangeUtcTicks { get; set; }
    [JsonProperty("next")] public long NextPollUtcTicks { get; set; }
    [JsonProperty("miss")] public int MissCount { get; set; }
    [JsonProperty("phase")] public string Phase { get; set; } = "Discovery";
    [JsonProperty("cycle")] public long? CycleTicks { get; set; }
}

public sealed class SchedulerSnapshotDto
{
    [JsonProperty("v")] public int Version { get; set; } = 1;
    [JsonProperty("states")] public Dictionary<string, ModelPollStateDto> States { get; set; } = new();
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

    public static SchedulerSnapshotDto ToSnapshot(Dictionary<string, ModelPollState> states)
    {
        var dto = new SchedulerSnapshotDto();
        foreach (var (key, state) in states)
        {
            dto.States[key] = new ModelPollStateDto
            {
                LastHash = state.LastHash,
                LastChangeUtcTicks = state.LastChangeUtc?.UtcTicks,
                PrevChangeUtcTicks = state.PrevChangeUtc?.UtcTicks,
                NextPollUtcTicks = state.NextPollUtc.UtcTicks,
                MissCount = state.MissCount,
                Phase = state.Phase.ToString(),
                CycleTicks = state.Cycle?.Ticks,
            };
        }
        return dto;
    }

    public static Dictionary<string, ModelPollState> FromSnapshot(SchedulerSnapshotDto dto)
    {
        var states = new Dictionary<string, ModelPollState>();
        foreach (var (key, s) in dto.States)
        {
            states[key] = new ModelPollState(
                LastHash: s.LastHash,
                LastChangeUtc: s.LastChangeUtcTicks.HasValue
                    ? new DateTimeOffset(s.LastChangeUtcTicks.Value, TimeSpan.Zero)
                    : null,
                PrevChangeUtc: s.PrevChangeUtcTicks.HasValue
                    ? new DateTimeOffset(s.PrevChangeUtcTicks.Value, TimeSpan.Zero)
                    : null,
                NextPollUtc: new DateTimeOffset(s.NextPollUtcTicks, TimeSpan.Zero),
                MissCount: s.MissCount,
                Phase: Enum.Parse<PollPhase>(s.Phase),
                Cycle: s.CycleTicks.HasValue
                    ? TimeSpan.FromTicks(s.CycleTicks.Value)
                    : null);
        }
        return states;
    }
}
