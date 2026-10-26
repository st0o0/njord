using Njord.Grpc.V2;

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
                settings["Njord:Weather:Models:0"] = Primary;
                settings["Njord:Weather:Models:1"] = Secondary;
                settings["Njord:Features:Weather:ModelComparison:Enabled"] = "true";
            },
            TestContext.Current.CancellationToken,
            [Primary, Secondary]);

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
        }

        _directory.Dispose();
    }

    [Fact(Timeout = 30_000)]
    public async Task Each_effective_model_is_served_separately_and_an_unknown_model_is_not_found()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = new WeatherService.WeatherServiceClient(Host.Channel);

        var primary = await Eventually.ValueAsync(async () =>
        {
            var response = await client.GetDomainDataAsync(new GetDomainDataRequest { Location = "luzern", Domain = Domain.Weather }, cancellationToken: ct);
            return response.Weather?.Points.Count > 0 ? response : null;
        }, ct);
        var secondary = await client.GetDomainDataAsync(
            new GetDomainDataRequest { Location = "luzern", Domain = Domain.Weather, Model = Secondary }, cancellationToken: ct);
        var unknown = await Assert.ThrowsAsync<global::Grpc.Core.RpcException>(async () =>
            await client.GetDomainDataAsync(new GetDomainDataRequest { Location = "luzern", Domain = Domain.Weather, Model = "icon_d2" }, cancellationToken: ct));

        Assert.Multiple(
            () => Assert.NotEqual(primary.Weather.Points[0].Temperature, secondary.Weather.Points[0].Temperature),
            () => Assert.Equal(global::Grpc.Core.StatusCode.NotFound, unknown.StatusCode));
    }
}
