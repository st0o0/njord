namespace Njord.Core.Domain;

public sealed record EntityMetadata(
    EntityPlatform Platform,
    string? DeviceClass = null,
    string? Icon = null,
    string? Unit = null);
