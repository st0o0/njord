using Njord.Messages.Trends;

namespace Njord.Mqtt.Payloads;

public sealed record TrendParameterPayload(TrendKey Key, TrendDirection? Direction, double? Delta);
