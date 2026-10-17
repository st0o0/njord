using System.Text.Json;
using System.Text.Json.Serialization;

namespace Njord.Mqtt.Serialization;

internal abstract class WriteOnlyJsonConverter<T> : JsonConverter<T>
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => throw new NotSupportedException("MQTT payloads are write-only.");
}
