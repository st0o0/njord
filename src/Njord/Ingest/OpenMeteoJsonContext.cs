using System.Text.Json;
using System.Text.Json.Serialization;

namespace Njord.Ingest;

[JsonSerializable(typeof(OpenMeteoForecastResponse))]
[JsonSerializable(typeof(OpenMeteoTimeSeries))]
[JsonSerializable(typeof(OpenMeteoErrorResponse))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(IReadOnlyList<long>))]
internal sealed partial class OpenMeteoJsonContext : JsonSerializerContext;
