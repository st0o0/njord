using Newtonsoft.Json;

namespace Njord.Domain.Analysis;

public sealed record DecayInfo(
    [property: JsonProperty("decayRate")] double DecayRate,
    [property: JsonProperty("reliableHours")] int? ReliableHours);
