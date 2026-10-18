using Njord.Messages.Sensors;
using Njord.Sensors;
using Njord.Weather;

namespace Njord.SensorHub.Tests;

public sealed class SensorHubStateSpec
{
    private static readonly DateTimeOffset Now = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Staleness = TimeSpan.FromMinutes(10);

    private static SensorReading Reading(
        SensorKind kind = SensorKind.IndoorTemperature,
        string location = "lucerne",
        string source = "sensor-1",
        double value = 22.5,
        DateTimeOffset? measuredAt = null) =>
        new(kind, location, source, value, measuredAt ?? Now);

    [Fact]
    public void Empty_state_has_no_readings()
    {
        Assert.Empty(SensorHubState.Empty.Readings);
    }

    [Fact]
    public void Apply_stores_reading()
    {
        var state = SensorHubState.Empty.Apply(Reading());

        Assert.Single(state.Readings);
        Assert.Equal(22.5, state.Readings[("lucerne", SensorKind.IndoorTemperature, "sensor-1")].Value);
    }

    [Fact]
    public void Apply_overwrites_reading_for_same_key()
    {
        var state = SensorHubState.Empty
            .Apply(Reading(value: 20.0))
            .Apply(Reading(value: 25.0));

        var item = Assert.Single(state.Readings);
        Assert.Equal(25.0, item.Value.Value);
    }

    [Fact]
    public void Apply_stores_multiple_keys()
    {
        var state = SensorHubState.Empty
            .Apply(Reading(kind: SensorKind.IndoorTemperature))
            .Apply(Reading(kind: SensorKind.IndoorHumidity, value: 55.0));

        Assert.Equal(2, state.Readings.Count);
    }

    [Fact]
    public void Validate_accepts_plausible_temperature()
    {
        var result = SensorHubState.Empty.Validate(Reading(value: 22.5));

        Assert.True(result.Accepted);
        Assert.Null(result.RejectionReason);
    }

    [Fact]
    public void Validate_rejects_out_of_range_temperature()
    {
        var result = SensorHubState.Empty.Validate(Reading(value: 100.0));

        Assert.False(result.Accepted);
        Assert.Contains("outside plausible range", result.RejectionReason);
    }

    [Fact]
    public void Validate_rejects_unknown_sensor_kind()
    {
        var result = SensorHubState.Empty.Validate(Reading(kind: (SensorKind)999));

        Assert.False(result.Accepted);
        Assert.Contains("Unknown SensorKind", result.RejectionReason);
    }

    [Fact]
    public void GetReadings_aggregates_fresh_readings()
    {
        var state = SensorHubState.Empty
            .Apply(Reading(source: "s1", value: 20.0))
            .Apply(Reading(source: "s2", value: 24.0));

        var response = state.GetReadings("lucerne", Now, Staleness);
        var result = Assert.IsType<SensorReadingsResult>(response, exactMatch: false);

        Assert.Equal(22.0, result.Sensors.Readings[SensorKind.IndoorTemperature].Value);
        Assert.Equal(2, result.Sensors.Readings[SensorKind.IndoorTemperature].SourceCount);
    }

    [Fact]
    public void GetReadings_excludes_expired_readings()
    {
        var expired = Now - Staleness - TimeSpan.FromSeconds(1);
        var state = SensorHubState.Empty
            .Apply(Reading(source: "s1", value: 20.0, measuredAt: expired))
            .Apply(Reading(source: "s2", value: 24.0, measuredAt: Now));

        var response = state.GetReadings("lucerne", Now, Staleness);
        var result = Assert.IsType<SensorReadingsResult>(response, exactMatch: false);

        Assert.Equal(24.0, result.Sensors.Readings[SensorKind.IndoorTemperature].Value);
        Assert.Equal(1, result.Sensors.Readings[SensorKind.IndoorTemperature].SourceCount);
    }

    [Fact]
    public void GetReadings_returns_not_available_when_all_expired()
    {
        var expired = Now - Staleness - TimeSpan.FromSeconds(1);
        var state = SensorHubState.Empty
            .Apply(Reading(measuredAt: expired));

        var response = state.GetReadings("lucerne", Now, Staleness);
        Assert.IsType<SensorReadingsNotAvailable>(response, exactMatch: false);
    }

    [Fact]
    public void GetReadings_returns_not_available_for_empty_state()
    {
        var response = SensorHubState.Empty.GetReadings("lucerne", Now, Staleness);
        Assert.IsType<SensorReadingsNotAvailable>(response, exactMatch: false);
    }

    [Fact]
    public void GetReadings_filters_by_location()
    {
        var state = SensorHubState.Empty
            .Apply(Reading(location: "lucerne"))
            .Apply(Reading(location: "zurich"));

        var response = state.GetReadings("lucerne", Now, Staleness);
        var result = Assert.IsType<SensorReadingsResult>(response, exactMatch: false);

        Assert.Equal("lucerne", result.Sensors.Location);
        Assert.Single(result.Sensors.Readings);
    }

    [Fact]
    public void Expire_removes_stale_readings()
    {
        var expired = Now - Staleness - TimeSpan.FromSeconds(1);
        var state = SensorHubState.Empty
            .Apply(Reading(source: "s1", measuredAt: expired))
            .Apply(Reading(source: "s2", measuredAt: Now));

        var result = state.Expire(Now, Staleness);

        Assert.Single(result.Readings);
        Assert.Equal("s2", result.Readings.Single().Value.Source);
    }

    [Fact]
    public void Expire_preserves_fresh_readings()
    {
        var state = SensorHubState.Empty
            .Apply(Reading(source: "s1"))
            .Apply(Reading(source: "s2"));

        var result = state.Expire(Now, Staleness);

        Assert.Equal(2, result.Readings.Count);
    }

    [Fact]
    public void Expire_returns_same_state_when_nothing_expired()
    {
        var state = SensorHubState.Empty.Apply(Reading());

        var result = state.Expire(Now, Staleness);

        Assert.Same(state, result);
    }

    [Fact]
    public void GetLocationReadings_maps_every_location_with_fresh_readings()
    {
        var state = SensorHubState.Empty
            .Apply(Reading(location: "lucerne", value: 22.0))
            .Apply(Reading(location: "bern", value: 19.0))
            .Apply(Reading(location: "zurich", value: 18.0, measuredAt: Now - TimeSpan.FromMinutes(20)));

        var readings = state.GetLocationReadings(Now, Staleness);

        Assert.Multiple(
            () => Assert.Equal(2, readings.ByLocation.Count),
            () => Assert.Equal(22.0, readings.For(new LocationId("lucerne"))?.Get(SensorKind.IndoorTemperature)),
            () => Assert.Equal(19.0, readings.For(new LocationId("bern"))?.Get(SensorKind.IndoorTemperature)),
            () => Assert.Null(readings.For(new LocationId("zurich"))));
    }

    [Fact]
    public void GetLocationReadings_aggregates_readings_whose_location_differs_only_in_case()
    {
        var state = SensorHubState.Empty
            .Apply(Reading(location: "lucerne", source: "a", value: 20.0))
            .Apply(Reading(location: "LUCERNE", source: "b", value: 24.0));

        var readings = state.GetLocationReadings(Now, Staleness);

        var entry = Assert.Single(readings.ByLocation).Value;
        var reading = entry.Readings[SensorKind.IndoorTemperature];
        Assert.Multiple(
            () => Assert.Equal(22.0, reading.Value),
            () => Assert.Equal(2, reading.SourceCount));
    }

    [Fact]
    public void GetLocationReadings_is_empty_for_an_empty_state()
    {
        Assert.Empty(SensorHubState.Empty.GetLocationReadings(Now, Staleness).ByLocation);
    }
}
