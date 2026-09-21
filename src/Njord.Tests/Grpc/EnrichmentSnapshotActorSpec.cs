using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Njord.Actors;
using Njord.Domain.Analysis;
using Njord.Grpc;
using Njord.Tests.Shared;

namespace Njord.Tests.Grpc;

public sealed class EnrichmentSnapshotActorSpec : Akka.Hosting.TestKit.TestKit
{
    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.AddTestPersistence();
    }

    private IActorRef CreateActor() =>
        Sys.ActorOf(Props.Create(() => new EnrichmentSnapshotActor()));

    [Fact(Timeout = 5000)]
    public async Task Update_and_retrieve_an_enrichment()
    {
        var actor = CreateActor();
        var result = new IndexResult("lucerne", [new DayScoreSet(0, 80, 90, 70, 85, 95, 60, 88, 75, HoursIncluded: 14)], null, null);

        var ack = await actor.Ask<Ack>(new UpdateEnrichment("lucerne", "indices", result), TestContext.Current.CancellationToken);
        Assert.NotNull(ack);

        var response = await actor.Ask<EnrichmentQueryResponse>(new QueryEnrichment("lucerne", "indices"), TestContext.Current.CancellationToken);
        var found = Assert.IsType<EnrichmentFound>(response);
        Assert.IsType<IndexResult>(found.Result);
    }

    [Fact(Timeout = 5000)]
    public async Task QueryAllEnrichments_returns_all_for_location()
    {
        var actor = CreateActor();
        await actor.Ask<Ack>(new UpdateEnrichment("lucerne", "indices",
            new IndexResult("lucerne", [new DayScoreSet(0, 80, 90, 70, 85, 95, 60, 88, 75, HoursIncluded: 14)], null, null)), TestContext.Current.CancellationToken);
        await actor.Ask<Ack>(new UpdateEnrichment("lucerne", "alerts",
            new AlertResult("lucerne", [])), TestContext.Current.CancellationToken);

        var response = await actor.Ask<AllEnrichmentsResult>(new QueryAllEnrichments("lucerne"), TestContext.Current.CancellationToken);
        Assert.Equal(2, response.Results.Count);
    }

    [Fact(Timeout = 5000)]
    public async Task Unknown_enrichment_returns_not_found()
    {
        var actor = CreateActor();

        var response = await actor.Ask<EnrichmentQueryResponse>(new QueryEnrichment("lucerne", "unknown"), TestContext.Current.CancellationToken);
        Assert.IsType<EnrichmentNotFound>(response);
    }
}
