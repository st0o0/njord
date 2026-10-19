using System.Collections.Concurrent;
using Akka.Actor;
using Grpc.Core;
using Microsoft.Extensions.Time.Testing;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Grpc.V2;
using Njord.Messages.Sensors;
using Njord.Tests.Shared;
using DomainSensorKind = Njord.Sensors.SensorKind;

namespace Njord.Grpc.Tests;

public sealed class SensorGrpcServiceSpec : NjordActorSpec
{
    private static new readonly DateTimeOffset Now = new(2026, 7, 28, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _time = new(Now);
    private readonly ConcurrentQueue<UpdateReading> _received = new();

    protected override bool UseTestPersistence => false;

    private SensorGrpcService CreateService(Func<UpdateReading, PushResult> hub, bool registerHub = true)
    {
        var serviceInfo = TestServiceInfo.Create(new NjordOptions
        {
            Locations = [new LocationOptions { Name = "Luzern", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2"],
        });
        if (registerHub)
        {
            var received = _received;
            ActorRegistry.Register<ISensorHub>(
                Sys.ActorOf(Props.Create(() => new FakeSensorHub(received, hub))), overwrite: true);
        }

        return new SensorGrpcService(ActorRegistry, _time, serviceInfo);
    }

    private static PushResult Accept(UpdateReading _) => new(true, null);

    private static SensorReading Reading(string location = "luzern", SensorKind kind = SensorKind.IndoorTemperature, double value = 21.5) =>
        new() { Kind = kind, Location = location, Value = value };

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Push_forwards_a_valid_reading_to_the_sensor_hub_under_the_configured_spelling()
    {
        var service = CreateService(Accept);

        var response = await service.Push(Reading(), TestServerCallContext.Create(TestContext.Current.CancellationToken));

        var command = Assert.Single(_received);
        Assert.Multiple(
            () => Assert.True(response.Accepted),
            () => Assert.Equal("Luzern", command.Reading.Location),
            () => Assert.Equal(DomainSensorKind.IndoorTemperature, command.Reading.Kind),
            () => Assert.Equal(21.5, command.Reading.Value),
            () => Assert.Equal(Now, command.Reading.MeasuredAt));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Push_relays_the_rejection_of_the_sensor_hub()
    {
        var service = CreateService(_ => new PushResult(false, "stale reading"));

        var response = await service.Push(Reading(), TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Multiple(
            () => Assert.False(response.Accepted),
            () => Assert.Equal("stale reading", response.RejectionReason));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Push_rejects_an_unknown_location_without_asking_the_sensor_hub()
    {
        var service = CreateService(Accept);

        var response = await service.Push(Reading(location: "atlantis"), TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Multiple(
            () => Assert.False(response.Accepted),
            () => Assert.Equal("Unknown location: atlantis", response.RejectionReason),
            () => Assert.Empty(_received));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Push_rejects_an_unspecified_sensor_kind()
    {
        var service = CreateService(Accept);

        var response = await service.Push(
            Reading(kind: SensorKind.Unspecified), TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Multiple(
            () => Assert.False(response.Accepted),
            () => Assert.Equal("Unknown or unspecified SensorKind: Unspecified", response.RejectionReason),
            () => Assert.Empty(_received));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Push_fails_with_unavailable_when_the_sensor_hub_is_not_registered()
    {
        var service = CreateService(Accept, registerHub: false);

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            service.Push(Reading(), TestServerCallContext.Create(TestContext.Current.CancellationToken)));

        Assert.Equal(StatusCode.Unavailable, ex.StatusCode);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task StreamPush_accepts_a_stream_of_valid_readings()
    {
        var service = CreateService(Accept);
        var stream = new ListStreamReader(Reading(value: 20), Reading(value: 21), Reading(value: 22));

        var response = await service.StreamPush(stream, TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Multiple(
            () => Assert.True(response.Accepted),
            () => Assert.Equal([20.0, 21.0, 22.0], _received.Select(c => c.Reading.Value).ToArray()));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task StreamPush_counts_invalid_and_rejected_readings()
    {
        var service = CreateService(c => c.Reading.Value > 21 ? new PushResult(false, "too high") : new PushResult(true, null));
        var stream = new ListStreamReader(
            Reading(value: 20), Reading(location: "atlantis"), Reading(value: 25), Reading(value: 21));

        var response = await service.StreamPush(stream, TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Multiple(
            () => Assert.False(response.Accepted),
            () => Assert.Equal("2/4 readings rejected", response.RejectionReason));
    }

    private sealed class FakeSensorHub : ReceiveActor
    {
        public FakeSensorHub(ConcurrentQueue<UpdateReading> received, Func<UpdateReading, PushResult> answer)
        {
            Receive<UpdateReading>(command =>
            {
                received.Enqueue(command);
                Sender.Tell(answer(command));
            });
        }
    }

    private sealed class ListStreamReader(params SensorReading[] readings) : IAsyncStreamReader<SensorReading>
    {
        private int _next = -1;

        public SensorReading Current => readings[_next];

        public Task<bool> MoveNext(CancellationToken cancellationToken) => Task.FromResult(++_next < readings.Length);
    }
}
