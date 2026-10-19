using Akka.Actor;
using Akka.Hosting;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Njord.Core.Actors;
using Njord.Grpc.V2;
using Njord.Tests.Shared;

namespace Njord.IntegrationTests.Grpc;

[Collection(HostCollection.Name)]
public sealed class GrpcChannelSpec : IAsyncLifetime
{
    private readonly TemporaryDirectory _directory = new();
    private NjordTestHost? _host;

    private NjordTestHost Host => _host ?? throw new InvalidOperationException("The host is not started.");

    public async ValueTask InitializeAsync() =>
        _host = await NjordTestHost.StartAsync(_directory.Path, cancellationToken: TestContext.Current.CancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
        }

        _directory.Dispose();
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task The_catalog_lists_the_configured_location()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = new WeatherService.WeatherServiceClient(Host.Channel);

        var catalog = await client.GetCatalogAsync(new GetCatalogRequest(), cancellationToken: ct);

        var location = Assert.Single(catalog.Locations);
        Assert.Equal("luzern", location.Name);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task The_status_reports_a_poll_state_for_every_configured_target()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = new OpsService.OpsServiceClient(Host.Channel);

        await Eventually.AssertAsync(async () =>
        {
            var status = await client.GetStatusAsync(new GetStatusRequest(), cancellationToken: ct);
            Assert.Equal(["luzern"], status.Models.Select(m => m.Location).Order());
        }, ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Weather_data_polled_through_the_fake_client_is_served_by_the_weather_service()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = new WeatherService.WeatherServiceClient(Host.Channel);

        var forecast = await Eventually.ValueAsync(async () =>
        {
            var response = await client.GetDomainDataAsync(new GetDomainDataRequest { Location = "luzern", Domain = Domain.Weather }, cancellationToken: ct);
            return response.Weather?.Points.Count > 0 ? response : null;
        }, ct);

        Assert.Multiple(
            () => Assert.Equal("luzern", forecast.Location),
            () => Assert.Equal(Domain.Weather, forecast.Domain));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_triggered_poll_is_streamed_to_a_domain_data_subscriber()
    {
        var ct = TestContext.Current.CancellationToken;
        var weather = new WeatherService.WeatherServiceClient(Host.Channel);
        var ops = new OpsService.OpsServiceClient(Host.Channel);
        using var call = weather.StreamDomainData(new StreamDomainDataRequest { Location = "luzern" }, cancellationToken: ct);

        await Eventually.AssertAsync(async () =>
        {
            var triggered = await ops.TriggerPollAsync(new TriggerPollRequest { Location = "luzern" }, cancellationToken: ct);
            Assert.Equal(1, triggered.TriggeredCount);
            using var next = CancellationTokenSource.CreateLinkedTokenSource(ct);
            next.CancelAfter(TimeSpan.FromSeconds(2));
            Assert.True(await call.ResponseStream.MoveNext(next.Token), "The stream ended before an update arrived.");
            Assert.Equal("luzern", call.ResponseStream.Current.Location);
        }, ct);
    }

    private sealed class SilentActor : ReceiveActor
    {
    }
}
