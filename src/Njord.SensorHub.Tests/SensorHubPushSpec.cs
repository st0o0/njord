using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.Time.Testing;
using Njord.Core.Actors;
using Njord.Core.Configuration.Sensors;
using Njord.Messages.Sensors;
using Njord.Sensors;
using Njord.Tests.Shared;
using Njord.Weather;

namespace Njord.SensorHub.Tests;

public sealed class SensorHubPushSpec : Akka.Hosting.TestKit.TestKit
{
    private readonly FakeTimeProvider _time = TestTimeouts.AtEpoch();

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.AddTestTimefactor();
    }

    private (IActorRef Hub, Akka.TestKit.TestProbe Pipeline) CreateHubWithPipeline(
        int stalenessSeconds = 3600, TimeSpan? expiryInterval = null)
    {
        var pipeline = CreateTestProbe();
        ActorRegistry.Register<IForecastPipeline>(pipeline.Ref, overwrite: true);
        var options = Microsoft.Extensions.Options.Options.Create(AppSettingsDefaults.Options<SensorOptions>(o => o.StalenessSeconds = stalenessSeconds));
        var interval = expiryInterval ?? TimeSpan.FromMinutes(10);
        var hub = Sys.ActorOf(Props.Create(() => new SensorHub(options, _time, (TimeSpan?)interval)), $"sensor-hub-{Guid.NewGuid():N}");
        return (hub, pipeline);
    }

    private SensorReading Reading(string location = "Luzern", string source = "wohnzimmer", double value = 23.5,
        DateTimeOffset? measuredAt = null) =>
        new(SensorKind.IndoorTemperature, location, source, value, measuredAt ?? _time.GetUtcNow());

    private async Task<PushResult> UpdateAsync(IActorRef hub, SensorReading reading, CancellationToken ct)
    {
        hub.Tell(new UpdateReading(reading), TestActor);
        return await ExpectMsgAsync<PushResult>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
    }

    private static async Task<LocationSensorReadings> NextPushAsync(
        Akka.TestKit.TestProbe pipeline, Func<LocationSensorReadings, bool> matches, CancellationToken ct) =>
        (LocationSensorReadings)await pipeline.FishForMessageAsync(
            m => m is LocationSensorReadings s && matches(s), TestTimeouts.AwaitAssertMax, cancellationToken: ct);

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task The_hub_pushes_its_map_once_the_pipeline_is_resolved()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, pipeline) = CreateHubWithPipeline();

        var push = await pipeline.ExpectMsgAsync<LocationSensorReadings>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);

        Assert.Empty(push.ByLocation);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_readings_request_is_answered_to_the_sender_with_the_current_map()
    {
        var ct = TestContext.Current.CancellationToken;
        var (hub, _) = CreateHubWithPipeline();
        await UpdateAsync(hub, Reading(location: "Luzern", value: 21.0), ct);
        var requester = CreateTestProbe();

        hub.Tell(new RequestSensorReadings(), requester.Ref);

        var reply = await requester.ExpectMsgAsync<LocationSensorReadings>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        var reading = Assert.Single(reply.ByLocation);
        Assert.Multiple(
            () => Assert.Equal(new LocationId("Luzern"), reading.Key),
            () => Assert.Equal(21.0, reading.Value.Get(SensorKind.IndoorTemperature)));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task An_accepted_reading_is_pushed_to_the_pipeline()
    {
        var ct = TestContext.Current.CancellationToken;
        var (hub, pipeline) = CreateHubWithPipeline();

        Assert.True((await UpdateAsync(hub, Reading(), ct)).Accepted);

        var push = await NextPushAsync(pipeline, s => s.ByLocation.Count > 0, ct);
        var reading = Assert.Single(push.ByLocation);
        Assert.Multiple(
            () => Assert.Equal(new LocationId("Luzern"), reading.Key),
            () => Assert.Equal(23.5, reading.Value.Get(SensorKind.IndoorTemperature)));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task The_pushed_map_covers_every_location_with_fresh_readings()
    {
        var ct = TestContext.Current.CancellationToken;
        var (hub, pipeline) = CreateHubWithPipeline();

        await UpdateAsync(hub, Reading(location: "Luzern", value: 23.5), ct);
        await UpdateAsync(hub, Reading(location: "Bern", value: 21.0), ct);

        var push = await NextPushAsync(pipeline, s => s.ByLocation.Count == 2, ct);
        Assert.Multiple(
            () => Assert.Equal(23.5, push.For(new LocationId("luzern"))?.Get(SensorKind.IndoorTemperature)),
            () => Assert.Equal(21.0, push.For(new LocationId("Bern"))?.Get(SensorKind.IndoorTemperature)));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_stale_reading_is_left_out_of_the_pushed_map()
    {
        var ct = TestContext.Current.CancellationToken;
        var (hub, pipeline) = CreateHubWithPipeline(stalenessSeconds: 3600);
        await pipeline.ExpectMsgAsync<LocationSensorReadings>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);

        await UpdateAsync(hub, Reading(location: "Luzern", measuredAt: _time.GetUtcNow().AddHours(-2)), ct);
        await UpdateAsync(hub, Reading(location: "Bern", value: 21.0), ct);

        var push = await NextPushAsync(pipeline, s => s.ByLocation.Count > 0, ct);
        var reading = Assert.Single(push.ByLocation);
        Assert.Equal(new LocationId("Bern"), reading.Key);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_reading_that_turned_stale_leaves_the_map_on_the_next_expiry_tick()
    {
        var ct = TestContext.Current.CancellationToken;
        var (hub, pipeline) = CreateHubWithPipeline(stalenessSeconds: 3600, expiryInterval: TimeSpan.FromMilliseconds(200));
        await UpdateAsync(hub, Reading(location: "Luzern"), ct);
        await NextPushAsync(pipeline, s => s.ByLocation.Count == 1, ct);

        _time.Advance(TimeSpan.FromHours(2));

        var push = await NextPushAsync(pipeline, s => s.ByLocation.Count == 0, ct);
        Assert.Empty(push.ByLocation);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_rejected_reading_pushes_nothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var (hub, pipeline) = CreateHubWithPipeline();
        await pipeline.ExpectMsgAsync<LocationSensorReadings>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);

        var result = await UpdateAsync(hub, Reading(value: 85.0), ct);

        Assert.False(result.Accepted);
        await pipeline.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), ct);
    }
}
