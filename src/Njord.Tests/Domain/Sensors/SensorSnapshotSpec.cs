using Njord.Domain.Sensors;

namespace Njord.Tests.Domain.Sensors;

public sealed class SensorSnapshotSpec
{
    [Fact(Timeout = 5000)]
    public void get_returns_value_for_existing_kind()
    {
        var snapshot = new SensorSnapshot("Luzern", new Dictionary<SensorKind, AggregatedReading>
        {
            [SensorKind.IndoorTemperature] = new(22.5, 1, DateTimeOffset.UtcNow),
        });

        Assert.Equal(22.5, snapshot.Get(SensorKind.IndoorTemperature));
    }

    [Fact(Timeout = 5000)]
    public void get_returns_null_for_missing_kind()
    {
        var snapshot = new SensorSnapshot("Luzern", new Dictionary<SensorKind, AggregatedReading>());

        Assert.Null(snapshot.Get(SensorKind.IndoorHumidity));
    }

    [Theory(Timeout = 5000)]
    [InlineData(SensorKind.IndoorTemperature, 23.5, true)]
    [InlineData(SensorKind.IndoorTemperature, -10.0, true)]
    [InlineData(SensorKind.IndoorTemperature, 60.0, true)]
    [InlineData(SensorKind.IndoorTemperature, -10.1, false)]
    [InlineData(SensorKind.IndoorTemperature, 60.1, false)]
    [InlineData(SensorKind.IndoorTemperature, 85.0, false)]
    [InlineData(SensorKind.IndoorHumidity, 50.0, true)]
    [InlineData(SensorKind.IndoorHumidity, -0.1, false)]
    public void plausibility_validates_against_kind_range(SensorKind kind, double value, bool expected)
    {
        var metadata = SensorKindMetadata.Get(kind);
        Assert.Equal(expected, metadata.IsPlausible(value));
    }

    [Fact(Timeout = 5000)]
    public void all_sensor_kinds_have_metadata()
    {
        foreach (var kind in Enum.GetValues<SensorKind>())
        {
            Assert.True(SensorKindMetadata.TryGet(kind, out var metadata), $"Missing metadata for {kind}");
            Assert.NotNull(metadata);
            Assert.Equal(kind, metadata!.Kind);
        }
    }
}
