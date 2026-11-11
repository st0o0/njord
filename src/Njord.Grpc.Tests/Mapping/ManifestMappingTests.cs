using Njord.Core.Domain;
using Njord.Grpc.Mapping;
using ProtoEntityPlatform = Njord.Grpc.Proto.EntityPlatform;
using ProtoOutputKind = Njord.Grpc.Proto.OutputKind;

namespace Njord.Grpc.Tests.Mapping;

public sealed class ManifestMappingTests
{
    [Fact]
    public void All_fields_map_with_full_entity_metadata()
    {
        var descriptor = new FeatureDescriptor(
            OutputKind.Alert, FeatureCategory.Forecast, "Frost",
            new DomainRequirements(),
            EntityMetadata: new EntityMetadata(EntityPlatform.Sensor, DeviceClass: "temperature", Icon: "mdi:thermometer", Unit: "°C"));
        var registration = new EntityRegistration("vienna_frost", "Vienna", descriptor);

        var proto = registration.ToEntityDefinition();

        Assert.Multiple(
            () => Assert.Equal("vienna_frost", proto.EntityId),
            () => Assert.Equal("Vienna", proto.Location),
            () => Assert.Equal("Frost", proto.Feature),
            () => Assert.Equal(ProtoEntityPlatform.Sensor, proto.Platform),
            () => Assert.Equal("temperature", proto.DeviceClass),
            () => Assert.Equal("mdi:thermometer", proto.Icon),
            () => Assert.Equal("°C", proto.Unit),
            () => Assert.Equal("forecast", proto.Category),
            () => Assert.Equal(ProtoOutputKind.Alert, proto.OutputKind));
    }

    [Fact]
    public void Null_entity_metadata_defaults_to_empty_strings_and_unspecified_platform()
    {
        var descriptor = new FeatureDescriptor(
            OutputKind.Score, FeatureCategory.Lifestyle, "Bbq",
            new DomainRequirements());
        var registration = new EntityRegistration("vienna_bbq", "Vienna", descriptor);

        var proto = registration.ToEntityDefinition();

        Assert.Multiple(
            () => Assert.Equal(ProtoEntityPlatform.Unspecified, proto.Platform),
            () => Assert.Equal("", proto.DeviceClass),
            () => Assert.Equal("", proto.Icon),
            () => Assert.Equal("", proto.Unit));
    }

    [Theory]
    [InlineData(EntityPlatform.Weather, ProtoEntityPlatform.Weather)]
    [InlineData(EntityPlatform.Sensor, ProtoEntityPlatform.Sensor)]
    [InlineData(EntityPlatform.BinarySensor, ProtoEntityPlatform.BinarySensor)]
    public void Entity_platform_maps_to_proto(EntityPlatform domain, ProtoEntityPlatform expected)
    {
        var descriptor = new FeatureDescriptor(
            OutputKind.Alert, FeatureCategory.Forecast, "Test",
            new DomainRequirements(),
            EntityMetadata: new EntityMetadata(domain));
        var registration = new EntityRegistration("test", "Test", descriptor);

        Assert.Equal(expected, registration.ToEntityDefinition().Platform);
    }

    [Theory]
    [InlineData(OutputKind.Alert, ProtoOutputKind.Alert)]
    [InlineData(OutputKind.Score, ProtoOutputKind.Score)]
    [InlineData(OutputKind.Condition, ProtoOutputKind.Condition)]
    [InlineData(OutputKind.Trend, ProtoOutputKind.Trend)]
    [InlineData(OutputKind.Forecast, ProtoOutputKind.Forecast)]
    [InlineData(OutputKind.Weather, ProtoOutputKind.Weather)]
    [InlineData(OutputKind.Observation, ProtoOutputKind.Observation)]
    public void Output_kind_maps_to_proto(OutputKind domain, ProtoOutputKind expected)
    {
        var descriptor = new FeatureDescriptor(
            domain, FeatureCategory.Forecast, "Test",
            new DomainRequirements());
        var registration = new EntityRegistration("test", "Test", descriptor);

        Assert.Equal(expected, registration.ToEntityDefinition().OutputKind);
    }
}
