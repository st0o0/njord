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

public sealed class WeatherGrpcServiceStreamEnrichmentsSpec : WeatherGrpcServiceSpecBase
{
    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task StreamEnrichments_streams_only_the_events_of_the_requested_location()
    {
        var service = CreateService(consumerActor: CreateStreamingReadModel());
        var writer = new CollectingStreamWriter<EnrichmentEvent>();

        await service.StreamEnrichments(new StreamEnrichmentsRequest { Location = "Lucerne" }, writer,
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Equal(
            [("lucerne", "alerts"), ("lucerne", "alerts")],
            writer.Messages.Select(m => (m.Location, m.TypeName)));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task StreamEnrichments_with_an_empty_location_streams_every_location()
    {
        var service = CreateService(consumerActor: CreateStreamingReadModel());
        var writer = new CollectingStreamWriter<EnrichmentEvent>();

        await service.StreamEnrichments(new StreamEnrichmentsRequest(), writer,
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Equal(
            [("lucerne", "alerts"), ("zurich", "alerts"), ("lucerne", "alerts")],
            writer.Messages.Select(m => (m.Location, m.TypeName)));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task StreamEnrichments_rejects_an_unknown_location_with_not_found()
    {
        var service = CreateService(consumerActor: CreateStreamingReadModel());
        var writer = new CollectingStreamWriter<EnrichmentEvent>();

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            service.StreamEnrichments(new StreamEnrichmentsRequest { Location = "atlantis" }, writer,
                TestServerCallContext.Create(TestContext.Current.CancellationToken)));

        Assert.Multiple(
            () => Assert.Equal(StatusCode.NotFound, ex.StatusCode),
            () => Assert.Equal("Location 'atlantis' not found", ex.Status.Detail),
            () => Assert.Empty(writer.Messages));
    }
}
