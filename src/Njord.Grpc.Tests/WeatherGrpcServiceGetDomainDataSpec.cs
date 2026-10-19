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

public sealed class WeatherGrpcServiceGetDomainDataSpec : WeatherGrpcServiceSpecBase
{
    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetDomainData_returns_the_latest_domain_data_with_timestamps()
    {
        var evt = new PipelineEvent.WeatherDataReceived(DomainData.Lucerne, DomainData.Weather(Anchor, 3));
        var actor = Sys.ActorOf(Props.Create(() => new FakeConsumerActor(evt)));
        var service = CreateService(consumerActor: actor);

        var response = await service.GetDomainData(
            new GetDomainDataRequest { Location = "lucerne", Domain = Domain.Weather },
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Multiple(
            () => Assert.Equal("Lucerne", response.Location),
            () => Assert.Equal(Domain.Weather, response.Domain),
            () => Assert.Equal(Anchor, response.ModelRun.ToDateTimeOffset()),
            () => Assert.Equal(3, response.Weather.Points.Count),
            () => Assert.Equal(Anchor.AddHours(1), response.Weather.Points[1].ValidAt.ToDateTimeOffset()),
            () => Assert.Equal(10.0, response.Weather.Points[0].Temperature),
            () => Assert.Equal(0.5, response.Weather.Points[0].EnsembleStdDev));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetDomainData_queries_the_configured_location_when_the_request_differs_in_case()
    {
        var evt = new PipelineEvent.FloodDataReceived(DomainData.Lucerne, DomainData.Flood(Anchor));
        var service = CreateService(consumerActor: Sys.ActorOf(Props.Create(() => new OrdinalGrpcConsumer(evt))));

        var response = await service.GetDomainData(
            new GetDomainDataRequest { Location = "LUCERNE", Domain = Domain.Flood },
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Multiple(
            () => Assert.Equal(Domain.Flood, response.Domain),
            () => Assert.Equal(12.5, Assert.Single(response.Flood.Points).Discharge));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetDomainData_throws_not_found_for_unknown_location()
    {
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            service.GetDomainData(
                new GetDomainDataRequest { Location = "unknown", Domain = Domain.Weather },
                TestServerCallContext.Create(TestContext.Current.CancellationToken)));

        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetDomainData_throws_not_found_before_the_first_data_of_the_domain()
    {
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            service.GetDomainData(
                new GetDomainDataRequest { Location = "lucerne", Domain = Domain.Solar },
                TestServerCallContext.Create(TestContext.Current.CancellationToken)));

        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetDomainData_rejects_an_unspecified_domain()
    {
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            service.GetDomainData(
                new GetDomainDataRequest { Location = "lucerne" },
                TestServerCallContext.Create(TestContext.Current.CancellationToken)));

        Assert.Multiple(
            () => Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode),
            () => Assert.Equal("Domain must be specified", ex.Status.Detail));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetDomainData_fails_with_unavailable_before_the_read_model_is_resolvable()
    {
        var serviceInfo = TestServiceInfo.Create(new NjordOptions
        {
            Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2"],
        });
        var service = new WeatherGrpcService(ActorRegistry, Sys, _time, serviceInfo);

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            service.GetDomainData(
                new GetDomainDataRequest { Location = "lucerne", Domain = Domain.Weather },
                TestServerCallContext.Create(TestContext.Current.CancellationToken)));

        Assert.Equal(StatusCode.Unavailable, ex.StatusCode);
    }
}
