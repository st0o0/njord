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

public sealed class WeatherGrpcServiceStreamDomainDataSpec : WeatherGrpcServiceSpecBase
{
    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task StreamDomainData_streams_only_the_events_of_the_requested_location()
    {
        var service = CreateService(consumerActor: CreateStreamingReadModel());
        var writer = new CollectingStreamWriter<DomainDataUpdate>();

        await service.StreamDomainData(new StreamDomainDataRequest { Location = "ZURICH" }, writer,
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        var update = Assert.Single(writer.Messages);
        Assert.Equal(("Zurich", Domain.Weather), (update.Location, update.Domain));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task StreamDomainData_filters_by_domain()
    {
        var service = CreateService(consumerActor: CreateStreamingReadModel());
        var writer = new CollectingStreamWriter<DomainDataUpdate>();
        var request = new StreamDomainDataRequest();
        request.Domains.Add(Domain.Flood);

        await service.StreamDomainData(request, writer, TestServerCallContext.Create(TestContext.Current.CancellationToken));

        var update = Assert.Single(writer.Messages);
        Assert.Equal(("Lucerne", Domain.Flood), (update.Location, update.Domain));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task StreamDomainData_without_filters_streams_every_domain_of_every_location()
    {
        var service = CreateService(consumerActor: CreateStreamingReadModel());
        var writer = new CollectingStreamWriter<DomainDataUpdate>();

        await service.StreamDomainData(new StreamDomainDataRequest(), writer,
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Equal(
            [("Lucerne", Domain.Weather), ("Zurich", Domain.Weather), ("Lucerne", Domain.Flood)],
            writer.Messages.Select(m => (m.Location, m.Domain)));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task StreamDomainData_rejects_an_unknown_location_with_not_found()
    {
        var service = CreateService(consumerActor: CreateStreamingReadModel());
        var writer = new CollectingStreamWriter<DomainDataUpdate>();

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            service.StreamDomainData(new StreamDomainDataRequest { Location = "atlantis" }, writer,
                TestServerCallContext.Create(TestContext.Current.CancellationToken)));

        Assert.Multiple(
            () => Assert.Equal(StatusCode.NotFound, ex.StatusCode),
            () => Assert.Equal("Location 'atlantis' not found", ex.Status.Detail),
            () => Assert.Empty(writer.Messages));
    }
}
