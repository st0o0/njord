using System.Text.Json.Serialization;
using Njord.Messages.Derived;
using Njord.Mqtt.Serialization;

namespace Njord.Mqtt.Payloads;

public sealed record DerivedHorizonPayload(
    int? Beaufort,
    double? WindChill,
    [property: JsonConverter(typeof(DewPointComfortWireConverter))] DewPointComfort? DewpointComfort,
    string? WmoDescription);
