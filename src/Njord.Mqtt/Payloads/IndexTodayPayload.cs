using Njord.Messages.Derived;

namespace Njord.Mqtt.Payloads;

public sealed record IndexTodayPayload(int? FrostHours, double? FrostConfidence, VpdCategory? VpdCategory, double? VpdKpa);
