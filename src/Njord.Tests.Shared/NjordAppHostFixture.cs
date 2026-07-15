using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Grpc.Net.Client;
using Njord.Configuration;
using RestEase;
using WireMock.Client;
using Xunit;

namespace Njord.Tests.Shared;

public sealed class NjordAppHostFixture : IAsyncLifetime
{
    private DistributedApplication? _app;

    public DistributedApplication App => _app
        ?? throw new InvalidOperationException("AppHost not initialized");

    public MqttOptions MqttOptions { get; private set; } = new();

    public GrpcChannel GrpcChannel { get; private set; } = null!;

    public IWireMockAdminApi WireMockAdmin { get; private set; } = null!;

    public string WireMockBaseUrl { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.Njord_AppHost>();

        _app = await builder.BuildAsync();

        using var startupCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        await _app.StartAsync(startupCts.Token);

        await _app.ResourceNotifications
            .WaitForResourceAsync("njord", "Running", startupCts.Token);

        var mqttEndpoint = _app.GetEndpoint("mosquitto", "mqtt");
        MqttOptions = new MqttOptions
        {
            Host = mqttEndpoint.Host,
            Port = mqttEndpoint.Port,
        };

        var wireMockEndpoint = _app.GetEndpoint("wiremock", "http");
        WireMockBaseUrl = wireMockEndpoint.ToString();
        WireMockAdmin = RestClient.For<IWireMockAdminApi>(WireMockBaseUrl);

        var njordEndpoint = _app.GetEndpoint("njord", "grpc");
        GrpcChannel = GrpcChannel.ForAddress(njordEndpoint, new GrpcChannelOptions
        {
            HttpHandler = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true,
            },
        });
    }

    public async ValueTask DisposeAsync()
    {
        GrpcChannel?.Dispose();
        if (_app is not null)
            await _app.DisposeAsync();
    }
}
