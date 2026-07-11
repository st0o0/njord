using System.Text.Json.Serialization;

namespace Njord.Ingest;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AdvancedForecastResponse))]
internal sealed partial class KachelmannJsonContext : JsonSerializerContext;
