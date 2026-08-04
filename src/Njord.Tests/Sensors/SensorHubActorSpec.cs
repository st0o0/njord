using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.Time.Testing;
using Njord.Configuration;
using Njord.Domain.Sensors;
using Njord.Sensors;

namespace Njord.Tests.Sensors;

public sealed class SensorHubActorSpec : Akka.Hosting.TestKit.TestKit
{
    private readonly FakeTimeProvider _time = new(DateTimeOffset.UtcNow);

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider) { }

    private IActorRef CreateHub(int stalenessSeconds = 7200)
    {
        var options = Microsoft.Extensions.Options.Options.Create(
            new SensorOptions { StalenessSeconds = stalenessSeconds });
        return Sys.ActorOf(Props.Create(() => new SensorHubActor(options, _time)));
    }

    private SensorReading Reading(
        SensorKind kind = SensorKind.IndoorTemperature,
        string location = "Luzern",
        string source = "wohnzimmer",
        double value = 23.5,
        DateTimeOffset? measuredAt = null)
        => new(kind, location, source, value, measuredAt ?? _time.GetUtcNow());

    [Fact(Timeout = 5000)]
    public async Task stores_and_retrieves_single_reading()
    {
        var hub = CreateHub();
        hub.Tell(new UpdateReading(Reading()), TestActor);
        var push = await ExpectMsgAsync<PushResult>();
        Assert.True(push.Accepted);

        hub.Tell(new GetSnapshot("Luzern"), TestActor);
        var response = await ExpectMsgAsync<SensorSnapshotResponse>();
        Assert.NotNull(response.Snapshot);
        Assert.Equal(23.5, response.Snapshot!.Get(SensorKind.IndoorTemperature));
    }

    [Fact(Timeout = 5000)]
    public async Task returns_null_snapshot_for_unknown_location()
    {
        var hub = CreateHub();
        hub.Tell(new GetSnapshot("Atlantis"), TestActor);
        var response = await ExpectMsgAsync<SensorSnapshotResponse>();
        Assert.Null(response.Snapshot);
    }

    [Fact(Timeout = 5000)]
    public async Task aggregates_multiple_sources_by_average()
    {
        var hub = CreateHub();
        hub.Tell(new UpdateReading(Reading(source: "wohnzimmer", value: 23.5)), TestActor);
        await ExpectMsgAsync<PushResult>();
        hub.Tell(new UpdateReading(Reading(source: "schlafzimmer", value: 21.0)), TestActor);
        await ExpectMsgAsync<PushResult>();

        hub.Tell(new GetSnapshot("Luzern"), TestActor);
        var response = await ExpectMsgAsync<SensorSnapshotResponse>();
        Assert.NotNull(response.Snapshot);

        var reading = response.Snapshot!.Readings[SensorKind.IndoorTemperature];
        Assert.Equal(22.25, reading.Value);
        Assert.Equal(2, reading.SourceCount);
    }

    [Fact(Timeout = 5000)]
    public async Task rejects_reading_outside_plausible_range()
    {
        var hub = CreateHub();
        hub.Tell(new UpdateReading(Reading(value: 85.0)), TestActor);
        var result = await ExpectMsgAsync<PushResult>();
        Assert.False(result.Accepted);
        Assert.Contains("outside plausible range", result.RejectionReason);
    }

    [Fact(Timeout = 5000)]
    public async Task excludes_stale_readings_from_snapshot()
    {
        var hub = CreateHub(stalenessSeconds: 3600);

        var oldTime = _time.GetUtcNow().AddHours(-2);
        hub.Tell(new UpdateReading(Reading(source: "old", value: 20.0, measuredAt: oldTime)), TestActor);
        await ExpectMsgAsync<PushResult>();

        hub.Tell(new UpdateReading(Reading(source: "fresh", value: 24.0)), TestActor);
        await ExpectMsgAsync<PushResult>();

        hub.Tell(new GetSnapshot("Luzern"), TestActor);
        var response = await ExpectMsgAsync<SensorSnapshotResponse>();
        var reading = response.Snapshot!.Readings[SensorKind.IndoorTemperature];
        Assert.Equal(24.0, reading.Value);
        Assert.Equal(1, reading.SourceCount);
    }

    [Fact(Timeout = 5000)]
    public async Task overwrites_same_source_with_newer_reading()
    {
        var hub = CreateHub();
        hub.Tell(new UpdateReading(Reading(value: 20.0)), TestActor);
        await ExpectMsgAsync<PushResult>();
        hub.Tell(new UpdateReading(Reading(value: 25.0)), TestActor);
        await ExpectMsgAsync<PushResult>();

        hub.Tell(new GetSnapshot("Luzern"), TestActor);
        var response = await ExpectMsgAsync<SensorSnapshotResponse>();
        Assert.Equal(25.0, response.Snapshot!.Get(SensorKind.IndoorTemperature));
    }
}
