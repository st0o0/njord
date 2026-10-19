using System.Text.Json.Serialization;

namespace Njord.OpenMeteo;

[JsonSerializable(typeof(OpenMeteoResponse))]
[JsonSerializable(typeof(OpenMeteoErrorResponse))]
[JsonSerializable(typeof(long[]))]
[JsonSerializable(typeof(double?[]))]
internal sealed partial class OpenMeteoJsonContext : JsonSerializerContext;
