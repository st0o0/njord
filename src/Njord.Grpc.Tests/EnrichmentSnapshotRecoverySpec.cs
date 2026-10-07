using Akka.Actor;
using Akka.Persistence.TestKit;
using Njord.Compute.Analysis;
using Njord.Messages.Common;
using Njord.Messages.Snapshots;

namespace Njord.Grpc.Tests;

public sealed class EnrichmentSnapshotRecoverySpec : PersistenceTestKit
{
    private const string EntityId = "lucerne|indices";

    private IActorRef CreateActor() =>
        Sys.ActorOf(Props.Create(() => new EnrichmentSnapshotActor(EntityId)));

    private static async Task FillToSnapshotThreshold(IActorRef actor, int count = 14, CancellationToken ct = default)
    {
        for (var i = 0; i < count; i++)
        {
            var result = new IndexResult("lucerne", [new DayScoreSet(0, 80 + i, 90, 70, 85, 95, 60, 88, 75, HoursIncluded: 14)], null, null);
            await actor.Ask<Ack>(new UpdateEnrichment("lucerne", "indices", result), ct);
        }
    }

    [Fact(Timeout = 5000)]
    public async Task State_recovers_from_snapshot_after_actor_restart()
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = CreateActor();
        await FillToSnapshotThreshold(actor, ct: ct);
        await actor.GracefulStop(TimeSpan.FromSeconds(3));

        var recovered = CreateActor();

        var response = await recovered.Ask<QueryEnrichmentResponse>(
            new QueryEnrichment("lucerne", "indices"), TimeSpan.FromSeconds(3), ct);
        Assert.IsType<EnrichmentFound>(response);
    }

    [Fact(Timeout = 5000)]
    public async Task Actor_accepts_updates_after_recovery()
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = CreateActor();
        await FillToSnapshotThreshold(actor, ct: ct);
        await actor.GracefulStop(TimeSpan.FromSeconds(3));

        var recovered = CreateActor();

        var ack = await recovered.Ask<Ack>(
            new UpdateEnrichment("lucerne", "indices", new IndexResult("lucerne", [new DayScoreSet(0, 99, 90, 70, 85, 95, 60, 88, 75, HoursIncluded: 14)], null, null)),
            TimeSpan.FromSeconds(3), ct);
        Assert.NotNull(ack);

        var response = await recovered.Ask<QueryEnrichmentResponse>(
            new QueryEnrichment("lucerne", "indices"), TimeSpan.FromSeconds(3), ct);
        var found = Assert.IsType<EnrichmentFound>(response);
        Assert.IsType<IndexResult>(found.Result);
    }

    [Fact(Timeout = 5000)]
    public async Task Snapshot_load_failure_during_recovery_kills_actor()
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = CreateActor();
        await FillToSnapshotThreshold(actor, ct: ct);
        await actor.GracefulStop(TimeSpan.FromSeconds(3));

        await WithSnapshotLoad(load => load.Fail(), async () =>
        {
            var recovered = CreateActor();
            await WatchAsync(recovered);
            await ExpectTerminatedAsync(recovered);
        });
    }
}
