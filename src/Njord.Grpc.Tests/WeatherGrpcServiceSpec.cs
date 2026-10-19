using Akka.Actor;
using Akka.Hosting;
using Grpc.Core;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Grpc.V2;
using Njord.Messages.Alerts;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public sealed class WeatherGrpcServiceSpec : Akka.Hosting.TestKit.TestKit
{
    private static readonly DateTimeOffset Anchor = TestTimeConstants.BudgetEpoch;
    private static readonly NjordOptions Options = new()
    {
        Locations = [new LocationOptions { Name = "Lucerne", Latitude = 47.05, Longitude = 8.31 }],
        Models = ["icon_d2", "ecmwf_ifs025"],
    };

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider) =>
        builder.AddTestTimefactor();

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetCatalog_returns_the_configured_locations_with_their_models()
    {
        var service = CreateService();

        var response = await service.GetCatalog(new GetCatalogRequest(), TestServerCallContext.Create(TestContext.Current.CancellationToken));

        var location = Assert.Single(response.Locations);
        Assert.Multiple(
            () => Assert.Equal("Lucerne", location.Name),
            () => Assert.Equal(47.05, location.Latitude),
            () => Assert.Equal(["icon_d2", "ecmwf_ifs025"], location.Models));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetDomainData_returns_the_latest_event_of_the_requested_domain()
    {
        var evt = new PipelineEvent.WeatherDataReceived(DomainData.Lucerne, DomainData.Weather(Anchor, 3));
        var service = CreateService(domainEvent: evt);

        var response = await service.GetDomainData(
            new GetDomainDataRequest { Location = "lucerne", Domain = Domain.Weather }, TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Multiple(
            () => Assert.Equal("Lucerne", response.Location),
            () => Assert.Equal(Domain.Weather, response.Domain),
            () => Assert.Equal(Anchor, response.ModelRun.ToDateTimeOffset()),
            () => Assert.Equal(3, response.Weather.Points.Count));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetDomainData_is_not_found_before_the_first_data_of_the_domain()
    {
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<RpcException>(() => service.GetDomainData(
            new GetDomainDataRequest { Location = "lucerne", Domain = Domain.Solar }, TestServerCallContext.Create(TestContext.Current.CancellationToken)));

        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetDomainData_rejects_an_unknown_location()
    {
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<RpcException>(() => service.GetDomainData(
            new GetDomainDataRequest { Location = "atlantis", Domain = Domain.Weather }, TestServerCallContext.Create(TestContext.Current.CancellationToken)));

        Assert.Multiple(
            () => Assert.Equal(StatusCode.NotFound, ex.StatusCode),
            () => Assert.Equal("Location 'atlantis' not found", ex.Status.Detail));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetDomainData_rejects_an_unspecified_domain()
    {
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<RpcException>(() => service.GetDomainData(
            new GetDomainDataRequest { Location = "lucerne" }, TestServerCallContext.Create(TestContext.Current.CancellationToken)));

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetEnrichments_returns_the_alerts_the_consumer_holds()
    {
        var alerts = new AlertResult(DomainData.Lucerne,
            [new Messages.Alerts.Alert(Messages.Alerts.AlertType.Frost, Messages.Alerts.AlertSeverity.Orange, 0.9, NoDetails.Instance)]);
        var service = CreateService(analysis: new LatestAnalysisResult(DomainData.Lucerne, alerts));

        var response = await service.GetEnrichments(new GetEnrichmentsRequest { Location = "lucerne" }, TestServerCallContext.Create(TestContext.Current.CancellationToken));

        var alert = Assert.Single(response.Alerts.Alerts);
        Assert.Multiple(
            () => Assert.Equal("Lucerne", response.Location),
            () => Assert.Equal(V2.AlertType.Frost, alert.Type),
            () => Assert.Equal(V2.AlertSeverity.Orange, alert.Severity));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetEnrichments_returns_only_the_location_before_the_first_analysis()
    {
        var service = CreateService();

        var response = await service.GetEnrichments(new GetEnrichmentsRequest { Location = "lucerne" }, TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Equal(new GetEnrichmentsResponse { Location = "Lucerne" }, response);
    }

    private WeatherGrpcService CreateService(PipelineEvent? domainEvent = null, LatestAnalysisResult? analysis = null)
    {
        ActorRegistry.Register<IGrpcConsumer>(
            Sys.ActorOf(Props.Create(() => new FakeConsumer(domainEvent, analysis))), overwrite: true);
        return new WeatherGrpcService(ActorRegistry, Sys, TimeProvider.System, TestServiceInfo.Create(Options));
    }

    private sealed class FakeConsumer : ReceiveActor
    {
        public FakeConsumer(PipelineEvent? domainEvent, LatestAnalysisResult? analysis)
        {
            Receive<QueryDomainData>(msg => Sender.Tell(domainEvent is null
                ? new DomainDataNotAvailable(msg.Location, msg.Domain)
                : new DomainDataResult(domainEvent)));
            Receive<QueryLatestAnalysis>(msg => Sender.Tell(
                analysis is null ? new LatestAnalysisNotAvailable(msg.Location) : analysis));
        }
    }
}
