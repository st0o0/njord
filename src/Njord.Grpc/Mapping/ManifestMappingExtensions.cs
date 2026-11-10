using Njord.Core.Domain;
using ProtoEntityPlatform = Njord.Grpc.Proto.EntityPlatform;
using ProtoOutputKind = Njord.Grpc.Proto.OutputKind;

namespace Njord.Grpc.Mapping;

internal static class ManifestMappingExtensions
{
    public static Proto.EntityDefinition ToEntityDefinition(this EntityRegistration r)
    {
        var meta = r.Descriptor.EntityMetadata;
        return new Proto.EntityDefinition
        {
            EntityId = r.EntityId,
            Location = r.Location,
            Feature = r.Descriptor.Name,
            Platform = MapPlatform(meta?.Platform),
            DeviceClass = meta?.DeviceClass ?? "",
            Unit = meta?.Unit ?? "",
            Icon = meta?.Icon ?? "",
            Category = r.Descriptor.Category.ToString().ToLowerInvariant(),
            OutputKind = MapOutputKind(r.Descriptor.Output),
        };
    }

    private static ProtoEntityPlatform MapPlatform(EntityPlatform? platform) => platform switch
    {
        EntityPlatform.Weather => ProtoEntityPlatform.Weather,
        EntityPlatform.Sensor => ProtoEntityPlatform.Sensor,
        EntityPlatform.BinarySensor => ProtoEntityPlatform.BinarySensor,
        _ => ProtoEntityPlatform.Unspecified,
    };

    private static ProtoOutputKind MapOutputKind(OutputKind kind) => kind switch
    {
        OutputKind.Alert => ProtoOutputKind.Alert,
        OutputKind.Score => ProtoOutputKind.Score,
        OutputKind.Condition => ProtoOutputKind.Condition,
        OutputKind.Trend => ProtoOutputKind.Trend,
        OutputKind.Forecast => ProtoOutputKind.Forecast,
        OutputKind.Weather => ProtoOutputKind.Weather,
        OutputKind.Observation => ProtoOutputKind.Observation,
        _ => ProtoOutputKind.Unspecified,
    };
}
