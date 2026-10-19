using System.Text.Json.Serialization;

namespace Njord.OpenMeteo;

internal sealed record OpenMeteoErrorResponse(
    [property: JsonPropertyName("error")] bool Error,
    [property: JsonPropertyName("reason")] string? Reason);
