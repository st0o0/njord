namespace Njord.Mqtt;

public sealed record SensorComponentSpec(
    string Key,
    string UniqueId,
    string Name,
    string StateTopic,
    string ValueTemplate,
    string Platform = "sensor",
    string? Unit = null,
    string? DeviceClass = null,
    int? SuggestedDisplayPrecision = null,
    string? JsonAttributesTopic = null,
    string? JsonAttributesTemplate = null,
    IReadOnlyList<(string Topic, string? ValueTemplate)>? ExtraAvailability = null,
    bool MeasurementLast = false);
