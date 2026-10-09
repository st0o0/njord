using Newtonsoft.Json;

namespace Njord.Persistence;

public sealed record SchedulerSnapshotDto
{
    [JsonProperty("states")] public Dictionary<string, ModelPollStateDto> States { get; init; } = new();
}
