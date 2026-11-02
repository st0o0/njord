using Njord.Core;
using Njord.Grpc.Mapping;
using Njord.Grpc.V2;
using Njord.Messages.Config;

namespace Njord.Grpc.Tests.Mapping;

public sealed class CatalogProtoMappingExtensionsTests
{
    [Fact]
    public void Location_with_resolved_domains_maps_to_proto()
    {
        var location = new CatalogLocation(
            new Location("Barcelona", 41.39, 2.17),
            ["best_match"],
            [
                new CatalogDomainSchema(DomainKind.AirQuality, ["pm25", "ozone"]),
                new CatalogDomainSchema(DomainKind.Marine, ["wave_height", "water_temperature"]),
            ]);

        var proto = location.ToProto();

        Assert.Equal(2, proto.ResolvedDomains.Count);

        Assert.Equal(Domain.AirQuality, proto.ResolvedDomains[0].Domain);
        Assert.Equal(["pm25", "ozone"], proto.ResolvedDomains[0].Fields);

        Assert.Equal(Domain.Marine, proto.ResolvedDomains[1].Domain);
        Assert.Equal(["wave_height", "water_temperature"], proto.ResolvedDomains[1].Fields);
    }

    [Fact]
    public void Location_without_resolved_domains_maps_empty()
    {
        var location = new CatalogLocation(
            new Location("Empty", 0, 0),
            ["best_match"],
            []);

        var proto = location.ToProto();

        Assert.Empty(proto.ResolvedDomains);
    }
}
