using Akka.Actor;
using Akka.Persistence.TestKit;
using Njord.Domain.Analysis;
using Njord.Grpc;

namespace Njord.Tests.Grpc;

public sealed class EnrichmentSnapshotRecoverySpec : PersistenceTestKit
{
    private IActorRef CreateActor() =>
        Sys.ActorOf(Props.Create(() => new EnrichmentSnapshotActor()));

    private static async Task FillToSnapshotThreshold(IActorRef actor, int count = 14)
    {
        for (var i = 0; i < count; i++)
        {
            var result = new IndexResult("lucerne", 80 + i, 90, 70, 85, 95, 60, 12.5, 0.5, 88, 75, null, null);
            await actor.Ask<Ack>(new UpdateEnrichment("lucerne", $"type_{i}", result));
        }
    }

    [Fact(Timeout = 5000)]
    public async Task State_recovers_from_snapshot_after_actor_restart()
    {
        var actor = CreateActor();
        await FillToSnapshotThreshold(actor);
        await actor.GracefulStop(TimeSpan.FromSeconds(3));

        var recovered = CreateActor();

        var response = await recovered.Ask<AllEnrichmentsResponse>(
            new GetAllEnrichments("lucerne"), TimeSpan.FromSeconds(3));
        Assert.Equal(14, response.Results.Count);
    }

    [Fact(Timeout = 5000)]
    public async Task Actor_accepts_updates_after_recovery()
    {
        var actor = CreateActor();
        await FillToSnapshotThreshold(actor);
        await actor.GracefulStop(TimeSpan.FromSeconds(3));

        var recovered = CreateActor();

        var ack = await recovered.Ask<Ack>(
            new UpdateEnrichment("zurich", "alerts", new AlertResult("zurich", [])),
            TimeSpan.FromSeconds(3));
        Assert.NotNull(ack);

        var response = await recovered.Ask<EnrichmentResponse>(
            new GetEnrichment("zurich", "alerts"), TimeSpan.FromSeconds(3));
        Assert.NotNull(response.Result);
        Assert.IsType<AlertResult>(response.Result);
    }

    [Fact(Timeout = 5000)]
    public async Task Snapshot_load_failure_during_recovery_kills_actor()
    {
        var actor = CreateActor();
        await FillToSnapshotThreshold(actor);
        await actor.GracefulStop(TimeSpan.FromSeconds(3));

        await WithSnapshotLoad(load => load.Fail(), async () =>
        {
            var recovered = CreateActor();
            Watch(recovered);
            await ExpectTerminatedAsync(recovered);
        });
    }
}
