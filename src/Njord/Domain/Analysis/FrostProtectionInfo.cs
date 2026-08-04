using Newtonsoft.Json;

namespace Njord.Domain.Analysis;

public sealed record FrostProtectionInfo(
    [property: JsonProperty("hoursUntilFrost")] int HoursUntilFrost,
    [property: JsonProperty("confidence")] double Confidence);
