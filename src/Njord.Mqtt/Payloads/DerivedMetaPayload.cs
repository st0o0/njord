namespace Njord.Mqtt.Payloads;

public sealed record DerivedMetaPayload(double? DiurnalAmplitude, double? SunshinePct, bool? Inversion);
