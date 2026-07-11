using System.Text.Json.Serialization;

namespace Njord.Ingest;

[JsonSerializable(typeof(OpenMeteoForecastResponse))]
[JsonSerializable(typeof(OpenMeteoErrorResponse))]
internal sealed partial class OpenMeteoJsonContext : JsonSerializerContext;
