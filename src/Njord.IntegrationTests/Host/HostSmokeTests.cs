using System.Net;
using Microsoft.Extensions.Options;

namespace Njord.IntegrationTests.Host;

[Collection(HostCollection.Name)]
public sealed class HostSmokeTests : IAsyncLifetime
{
    private readonly TemporaryDirectory _directory = new();
    private NjordTestHost? _host;

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

    [Fact(Timeout = 30_000)]
    public async Task The_liveness_endpoint_answers_ok()
    {
        Assert.NotNull(_host);

        var response = await _host.Http.GetAsync("/alive", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(Timeout = 30_000)]
    public async Task The_health_endpoint_reports_the_started_service_as_not_unhealthy()
    {
        Assert.NotNull(_host);

        var response = await _host.Http.GetAsync("/healthz", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(Timeout = 30_000)]
    public async Task The_metrics_endpoint_exposes_the_njord_meters()
    {
        Assert.NotNull(_host);
        var ct = TestContext.Current.CancellationToken;

        var response = await _host.Http.GetAsync("/metrics", ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.OK, response.StatusCode),
            () => Assert.Contains("njord_", body));
    }

    [Fact(Timeout = 30_000)]
    public async Task A_configuration_without_locations_fails_startup_with_the_validation_message()
    {
        Assert.NotNull(_host);
        await _host.DisposeAsync();
        _host = null;
        using var directory = new TemporaryDirectory();

        var ex = await Assert.ThrowsAsync<OptionsValidationException>(() => NjordTestHost.StartAsync(
            directory.Path,
            settings => settings.Remove("Njord:Locations:0:Name"),
            TestContext.Current.CancellationToken));

        Assert.Contains("Every location needs a non-empty Name.", ex.Message);
    }
}
