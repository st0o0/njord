using Njord.Grpc.Proto;

namespace Njord.IntegrationTests.Grpc;

[Collection(HostCollection.Name)]
public sealed class MultiModelTests : IAsyncLifetime
{
    private const string Primary = "ecmwf_ifs025_ensemble_mean";
    private const string Secondary = "ecmwf_aifs025_ensemble_mean";

    private readonly TemporaryDirectory _directory = new();
    private NjordTestHost? _host;

    private NjordTestHost Host => _host ?? throw new InvalidOperationException("The host is not started.");

    public async ValueTask InitializeAsync() =>
        _host = await NjordTestHost.StartAsync(
            _directory.Path,
            settings =>
            {
                settings["Njord:Ensemble:Models:0"] = Primary;
                settings["Njord:Ensemble:Models:1"] = Secondary;
                settings["Njord:Features:EnsembleModelComparison:Enabled"] = "true";
            },
            TestContext.Current.CancellationToken,
            [Primary, Secondary]).ConfigureAwait(false);

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync().ConfigureAwait(false);
        }

        _directory.Dispose();
    }

    [Fact(Timeout = 30_000)]
    public async Task Each_configured_model_creates_a_separate_weather_entity()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = new NjordService.NjordServiceClient(Host.Channel);

        var manifest = await client.GetManifestAsync(new ManifestRequest(), cancellationToken: ct);

        var weatherEntities = manifest.Entities
            .Where(e => e.OutputKind == OutputKind.Weather)
            .Select(e => e.EntityId)
            .Order()
            .ToList();

        Assert.Multiple(
            () => Assert.Contains(weatherEntities, id => id.Contains("luzern") && !id.Contains("ensemble")),
            () => Assert.Contains(weatherEntities, id => id.Contains("ecmwf_ifs025_ensemble_mean")),
            () => Assert.Contains(weatherEntities, id => id.Contains("ecmwf_aifs025_ensemble_mean")));
    }
}
