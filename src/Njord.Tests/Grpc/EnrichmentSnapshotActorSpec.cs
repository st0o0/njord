using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        var result = new IndexResult("lucerne", 80, 90, 70, 85, 95, 60, 12.5, 0.5, 88, 75, null, null);

        var ack = await actor.Ask<Ack>(new UpdateEnrichment("lucerne", "indices", result));
        Assert.NotNull(ack);

        var response = await actor.Ask<EnrichmentResponse>(new GetEnrichment("lucerne", "indices"));
        Assert.NotNull(response.Result);
        Assert.IsType<IndexResult>(response.Result);
    }

    [Fact(Timeout = 5000)]
    public async Task GetAllEnrichments_returns_all_for_location()
    {
        var actor = CreateActor();
        await actor.Ask<Ack>(new UpdateEnrichment("lucerne", "indices",
            new IndexResult("lucerne", 80, 90, 70, 85, 95, 60, 12.5, 0.5, 88, 75, null, null)));
        await actor.Ask<Ack>(new UpdateEnrichment("lucerne", "alerts",
            new AlertResult("lucerne", [])));

        var response = await actor.Ask<AllEnrichmentsResponse>(new GetAllEnrichments("lucerne"));
        Assert.Equal(2, response.Results.Count);
    }

    [Fact(Timeout = 5000)]
    public async Task Unknown_enrichment_returns_null()
    {
        var actor = CreateActor();

        var response = await actor.Ask<EnrichmentResponse>(new GetEnrichment("lucerne", "unknown"));
        Assert.Null(response.Result);
    }
}
