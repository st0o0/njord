using Akka.Actor;
using Grpc.Core;
using Njord.Core.Configuration;
using Njord.Grpc.V2;
using Njord.Messages.Alerts;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public sealed class WeatherGrpcServiceGetEnrichmentsSpec : WeatherGrpcServiceSpecBase
{
    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetEnrichments_returns_only_the_location_before_the_first_analysis()
    {
        var service = CreateService();

        var response = await service.GetEnrichments(
            new GetEnrichmentsRequest { Location = "lucerne" },
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Equal(new GetEnrichmentsResponse { Location = "lucerne" }, response);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetEnrichments_returns_the_alerts_the_read_model_holds()
    {
        var lucerne = new Location("lucerne", 0, 0);
        var analysis = new LatestAnalysisResult(lucerne, new AlertResult(lucerne, []));
        var service = CreateService(consumerActor: Sys.ActorOf(Props.Create(() => new FakeGrpcConsumer(analysis, Array.Empty<PipelineEvent>()))));

        var response = await service.GetEnrichments(
            new GetEnrichmentsRequest { Location = "lucerne" },
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Equal(new GetEnrichmentsResponse { Location = "lucerne", Alerts = new AlertUpdate() }, response);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetEnrichments_queries_the_configured_location_when_the_request_differs_in_case()
    {
        var lucerne = new Location("lucerne", 0, 0);
        var analysis = new LatestAnalysisResult(lucerne, new AlertResult(lucerne, []));
        var service = CreateService(consumerActor: Sys.ActorOf(Props.Create(() => new FakeGrpcConsumer(analysis, Array.Empty<PipelineEvent>()))));

        var response = await service.GetEnrichments(
            new GetEnrichmentsRequest { Location = "LUCERNE" },
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Multiple(
            () => Assert.Equal("lucerne", response.Location),
            () => Assert.NotNull(response.Alerts));
    }
}
