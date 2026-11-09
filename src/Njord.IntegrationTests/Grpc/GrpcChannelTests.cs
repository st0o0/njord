using Njord.Grpc.Proto;

namespace Njord.IntegrationTests.Grpc;

[Collection(HostCollection.Name)]
public sealed class GrpcChannelTests : IAsyncLifetime
{
    private readonly TemporaryDirectory _directory = new();
    private NjordTestHost? _host;

    private NjordTestHost Host => _host ?? throw new InvalidOperationException("The host is not started.");

    public async ValueTask InitializeAsync() =>
        _host = await NjordTestHost.StartAsync(_directory.Path, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(false);

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync().ConfigureAwait(false);
        }

        _directory.Dispose();
    }

    [Fact(Timeout = 30_000)]
    public async Task The_manifest_lists_the_configured_location()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = new NjordService.NjordServiceClient(Host.Channel);

        var manifest = await client.GetManifestAsync(new ManifestRequest(), cancellationToken: ct);

        Assert.Contains(manifest.Entities, e => e.Location == "luzern");
    }

    [Fact(Timeout = 30_000)]
    public async Task The_status_reports_a_poll_state_for_every_configured_target()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = new NjordService.NjordServiceClient(Host.Channel);

        await Eventually.AssertAsync(async () =>
        {
            var status = await client.GetStatusAsync(new StatusRequest(), cancellationToken: ct);
            Assert.Equal(["luzern"], status.Models.Select(m => m.Location).Order());
        }, ct);
    }

    [Fact(Timeout = 30_000)]
    public async Task Entity_updates_are_delivered_via_subscribe()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = new NjordService.NjordServiceClient(Host.Channel);

        await Eventually.AssertAsync(async () =>
        {
            using var call = client.Subscribe(new SubscribeRequest { Location = "luzern" }, cancellationToken: ct);
            await client.TriggerPollAsync(new TriggerPollRequest { Location = "luzern" }, cancellationToken: ct);
            using var next = CancellationTokenSource.CreateLinkedTokenSource(ct);
            next.CancelAfter(TimeSpan.FromSeconds(2));
            Assert.True(await call.ResponseStream.MoveNext(next.Token).ConfigureAwait(false), "The stream ended before an update arrived.");
        }, ct);
    }
}
