using Akka.Actor;
using Akka.TestKit;
using Njord.Grpc.V2;
using Njord.Messages.Pipeline;
using Njord.IntegrationTests.Infrastructure;

namespace Njord.IntegrationTests.Grpc;

[Collection("Ops")]
public sealed class OpsGrpcIntegrationSpec
{
    private readonly NjordFixture _fixture;

    public OpsGrpcIntegrationSpec(NjordFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(Timeout = 15_000)]
    public async Task TriggerPoll_sends_TriggerImmediatePoll_to_Scheduler_probe()
    {
        var client = new OpsService.OpsServiceClient(_fixture.GrpcChannel);

        var request = new TriggerPollRequest
        {
            Location = "lucerne",
            Model = "icon_d2",
        };

        var callTask = Task.Run(
            async () => await client.TriggerPollAsync(request),
            TestContext.Current.CancellationToken);

        var msg = _fixture.SchedulerProbe.ExpectMsg<TriggerImmediatePoll>(TimeSpan.FromSeconds(5));
        Assert.Equal("lucerne", msg.Location);
        Assert.Equal("icon_d2", msg.Model);
        _fixture.SchedulerProbe.Sender.Tell(
            new TriggerImmediatePollResult(1, ["lucerne/icon_d2"]), ActorRefs.NoSender);

        var response = await callTask;
        Assert.Equal(1, response.TriggeredCount);
    }
}
