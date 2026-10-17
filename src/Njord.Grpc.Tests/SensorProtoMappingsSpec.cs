using Njord.Grpc.Mapping;
using Njord.Grpc.V2;
using Njord.Messages.Config;
using Njord.Weather;
using DomainSensorKind = Njord.Sensors.SensorKind;

namespace Njord.Grpc.Tests;

public sealed class SensorProtoMappingsSpec
{
    private static readonly DateTimeOffset Now = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    private static readonly CatalogResult Catalog = new(
        [new CatalogLocation(new LocationId("Luzern"), 47.05, 8.31, ["icon_d2"])],
        []);

    [Fact]
    public void A_reading_is_forwarded_under_the_configured_location_spelling()
    {
        var request = new SensorReading
        {
            Kind = SensorKind.IndoorTemperature,
            Location = "luzern",
            Source = "wohnzimmer",
            Value = 23.5,
        };

        var (command, rejection) = request.ToCommand(Catalog, Now);

        Assert.Null(rejection);
        Assert.NotNull(command);
        Assert.Multiple(
            () => Assert.Equal("Luzern", command.Reading.Location),
            () => Assert.Equal(DomainSensorKind.IndoorTemperature, command.Reading.Kind),
            () => Assert.Equal("wohnzimmer", command.Reading.Source),
            () => Assert.Equal(23.5, command.Reading.Value),
            () => Assert.Equal(Now, command.Reading.MeasuredAt));
    }

    [Fact]
    public void A_reading_without_source_uses_the_default_source()
    {
        var request = new SensorReading
        {
            Kind = SensorKind.IndoorTemperature,
            Location = "luzern",
            Value = 21.0,
        };

        var (command, rejection) = request.ToCommand(Catalog, Now);

        Assert.Null(rejection);
        Assert.NotNull(command);
        Assert.Equal(SensorProtoMappings.DefaultSource, command.Reading.Source);
        Assert.Equal("default", SensorProtoMappings.DefaultSource);
    }
}
