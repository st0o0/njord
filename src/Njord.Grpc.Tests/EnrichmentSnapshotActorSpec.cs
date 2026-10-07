using Akka.Actor;
using Akka.Hosting;
using Njord.Compute.Analysis;
using Njord.Messages.Common;
using Njord.Messages.Snapshots;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public sealed class EnrichmentSnapshotActorSpec : Akka.Hosting.TestKit.TestKit
{
    private const string EntityId = "lucerne|indices";

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder
            .AddTestPersistence()
            .AddTestTimefactor();
    }

    private IActorRef CreateActor(string entityId = EntityId) =>
        Sys.ActorOf(Props.Create(() => new EnrichmentSnapshotActor(entityId)));

    [Fact(Timeout = 5000)]
    public async Task Update_and_retrieve_an_enrichment()
    {
        var actor = CreateActor();
        var result = new IndexResult("lucerne", [new DayScoreSet(0, 80, 90, 70, 85, 95, 60, 88, 75, HoursIncluded: 14)], null, null);

        var ack = await actor.Ask<Ack>(new UpdateEnrichment("lucerne", "indices", result), TestContext.Current.CancellationToken);
        Assert.NotNull(ack);

        var response = await actor.Ask<QueryEnrichmentResponse>(new QueryEnrichment("lucerne", "indices"), TestContext.Current.CancellationToken);
        var found = Assert.IsType<EnrichmentFound>(response);
        Assert.IsType<IndexResult>(found.Result);
    }

    [Fact(Timeout = 5000)]
    public async Task Unknown_enrichment_returns_not_found()
    {
        var actor = CreateActor();

        var response = await actor.Ask<QueryEnrichmentResponse>(new QueryEnrichment("lucerne", "indices"), TestContext.Current.CancellationToken);
        Assert.IsType<EnrichmentNotFound>(response);
    }
}
