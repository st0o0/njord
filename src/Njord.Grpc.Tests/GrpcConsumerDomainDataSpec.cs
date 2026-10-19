using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Core;
using Njord.Core.Actors;
using Njord.Messages.Alerts;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public sealed class GrpcConsumerDomainDataSpec : GrpcConsumerSpecBase
{
    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_domain_data_query_returns_the_latest_event_of_the_location_and_domain()
    {
        var ct = TestContext.Current.CancellationToken;
        var (readModel, pipeline) = await StartAsync();
        var older = new PipelineEvent.WeatherDataReceived(DomainData.Lucerne, DomainData.Weather(Anchor, 2));
        var newer = new PipelineEvent.WeatherDataReceived(DomainData.Lucerne, DomainData.Weather(Anchor.AddHours(1), 2));
        var flood = new PipelineEvent.FloodDataReceived(DomainData.Lucerne, DomainData.Flood(Anchor));

        await pipeline.OfferAsync(older, ct);
        await pipeline.OfferAsync(flood, ct);
        await pipeline.OfferAsync(newer, ct);

        await AwaitAssertAsync(async () =>
        {
            var weather = await readModel.Ask<DomainDataResponse>(new QueryDomainData(Lucerne, DomainKind.Weather), AskTimeout, ct);
            var floodReply = await readModel.Ask<DomainDataResponse>(new QueryDomainData(Lucerne, DomainKind.Flood), AskTimeout, ct);
            Assert.Multiple(
                () => Assert.Equal(new DomainDataResult(newer), weather),
                () => Assert.Equal(new DomainDataResult(flood), floodReply));
        }, TestTimeouts.AwaitAssertMax, cancellationToken: ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Domain_data_is_kept_per_location()
    {
        var ct = TestContext.Current.CancellationToken;
        var (readModel, pipeline) = await StartAsync();
        var lucerne = new PipelineEvent.WeatherDataReceived(DomainData.Lucerne, DomainData.Weather(Anchor, 2));

        await pipeline.OfferAsync(lucerne, ct);

        await AwaitAssertAsync(async () =>
        {
            var found = await readModel.Ask<DomainDataResponse>(new QueryDomainData(new Location("LUCERNE", 0, 0), DomainKind.Weather), AskTimeout, ct);
            var missing = await readModel.Ask<DomainDataResponse>(new QueryDomainData(Zurich, DomainKind.Weather), AskTimeout, ct);
            Assert.Multiple(
                () => Assert.Equal(new DomainDataResult(lucerne), found),
                () => Assert.Equal(new DomainDataNotAvailable(Zurich, DomainKind.Weather), missing));
        }, TestTimeouts.AwaitAssertMax, cancellationToken: ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Queries_before_the_first_cycle_report_that_no_data_is_available()
    {
        var ct = TestContext.Current.CancellationToken;
        var (readModel, _) = await StartAsync();

        var domainData = await readModel.Ask<DomainDataResponse>(new QueryDomainData(Lucerne, DomainKind.Weather), AskTimeout, ct);
        var analysis = await readModel.Ask<LatestAnalysisResponse>(new QueryLatestAnalysis(Lucerne), AskTimeout, ct);

        Assert.Equal(new DomainDataNotAvailable(Lucerne, DomainKind.Weather), domainData);
        Assert.Equal(new LatestAnalysisNotAvailable(Lucerne), analysis);
    }
}
