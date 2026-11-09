using Njord.Core.Domain;

namespace Njord.Core.Tests.Domain;

public sealed class EntityMetadataTests
{
    [Fact]
    public void Construction_with_all_fields()
    {
        var meta = new EntityMetadata(EntityPlatform.Sensor, "pm25", "mdi:molecule", "µg/m³");

        Assert.Equal(EntityPlatform.Sensor, meta.Platform);
        Assert.Equal("pm25", meta.DeviceClass);
        Assert.Equal("mdi:molecule", meta.Icon);
        Assert.Equal("µg/m³", meta.Unit);
    }

    [Fact]
    public void Construction_with_defaults()
    {
        var meta = new EntityMetadata(EntityPlatform.BinarySensor);

        Assert.Equal(EntityPlatform.BinarySensor, meta.Platform);
        Assert.Null(meta.DeviceClass);
        Assert.Null(meta.Icon);
        Assert.Null(meta.Unit);
    }

    [Fact]
    public void Record_equality()
    {
        var a = new EntityMetadata(EntityPlatform.Sensor, "temperature", null, "°C");
        var b = new EntityMetadata(EntityPlatform.Sensor, "temperature", null, "°C");

        Assert.Equal(a, b);
    }

    [Fact]
    public void Record_inequality_on_different_platform()
    {
        var a = new EntityMetadata(EntityPlatform.Sensor);
        var b = new EntityMetadata(EntityPlatform.BinarySensor);

        Assert.NotEqual(a, b);
    }
}
