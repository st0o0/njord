using Njord.Core.Solar;
using Njord.Grpc.Mapping;

namespace Njord.Grpc.Tests;

public sealed class SatelliteSampleMappingsTests
{
    private static readonly DateTimeOffset At = new(2026, 7, 1, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Every_irradiance_component_maps_to_its_own_field()
    {
        var sample = new SatelliteRadiationPoint(At, GHI: 340, DirectRadiation: 250, DiffuseRadiation: 90, DNI: 500, GTI: 380).ToProto();

        Assert.Multiple(
            () => Assert.Equal(340, sample.Ghi),
            () => Assert.Equal(250, sample.DirectRadiation),
            () => Assert.Equal(90, sample.DiffuseRadiation),
            () => Assert.Equal(500, sample.Dni),
            () => Assert.Equal(380, sample.Gti));
    }

    [Fact]
    public void Missing_components_stay_unset()
    {
        var sample = new SatelliteRadiationPoint(At, GTI: 380).ToProto();

        Assert.Multiple(
            () => Assert.True(sample.HasGti),
            () => Assert.False(sample.HasGhi),
            () => Assert.False(sample.HasDni));
    }
}
