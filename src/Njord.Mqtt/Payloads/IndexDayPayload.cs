using System.Text.Json.Serialization;
using Njord.Mqtt.Serialization;

namespace Njord.Mqtt.Payloads;

[JsonConverter(typeof(IndexDayPayloadConverter))]
public sealed record IndexDayPayload(IReadOnlyList<IndexScorePayload> Scores, int HoursIncluded, IndexTodayPayload? Today);
