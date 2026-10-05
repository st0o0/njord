using Akka.Actor;
using Akka.TestKit;
using Njord.Grpc.V2;
using Njord.IntegrationTests.Infrastructure;
using Njord.Messages.Sensors;

namespace Njord.IntegrationTests.Grpc;

[Collection("Sensor")]
public sealed class SensorGrpcIntegrationSpec
{
    private readonly NjordFixture _fixture;

    public SensorGrpcIntegrationSpec(NjordFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(Timeout = 15_000)]
    public async Task Push_sends_UpdateReading_to_SensorHub_probe()
    {
        var client = new SensorService.SensorServiceClient(_fixture.GrpcChannel);

        var reading = new SensorReading
        {
            Kind = SensorKind.IndoorTemperature,
            Value = 22.5,
            Location = "lucerne",
        };

        var callTask = Task.Run(async () => await client.PushAsync(reading), TestContext.Current.CancellationToken);

        var msg = _fixture.SensorHubProbe.ExpectMsg<UpdateReading>(TimeSpan.FromSeconds(5), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("lucerne", msg.Reading.Location);
        _fixture.SensorHubProbe.Sender.Tell(new PushResult(true, ""), ActorRefs.NoSender);

        var response = await callTask;
        Assert.True(response.Accepted);
    }
}
