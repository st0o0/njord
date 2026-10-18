using System.Net;
using System.Net.Sockets;
using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Njord.IntegrationTests.Broker;

// One Mosquitto broker per test class, anonymous access, no persistence (a restart drops the retained messages).
// The image is pinned by tag and digest. Without a reachable Docker daemon the fixture fails with a descriptive
// message instead of skipping, like every other test in this repository.
public sealed class MosquittoFixture : IAsyncLifetime
{
    private const string Image = "eclipse-mosquitto:2.0.20@sha256:21421af7b32bf9ce508e9090c8eb13bb81f410ca778dc205506180a6f862d0eb";
    private const string Config = "listener 1883\nallow_anonymous true\npersistence false\n";

    private IContainer? _container;

    // Fixed for the lifetime of the fixture, so the address survives a broker restart.
    public int Port { get; } = FreePort();

    public async ValueTask InitializeAsync()
    {
        try
        {
            _container = new ContainerBuilder(Image)
                .WithPortBinding(Port, 1883)
                .WithResourceMapping(Encoding.UTF8.GetBytes(Config), "/mosquitto/config/mosquitto.conf")
                .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("mosquitto version 2.0.20 running"))
                .Build();
            await _container.StartAsync(TestContext.Current.CancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                "The MQTT broker tests need a running Docker daemon (Testcontainers could not start the Mosquitto container). "
                + "Start Docker, or run the Docker-free part only: dotnet run --project Njord.IntegrationTests -- -namespace Njord.IntegrationTests.Host",
                ex);
        }
    }

    public async Task RestartAsync(CancellationToken cancellationToken)
    {
        Assert.NotNull(_container);
        await _container.StopAsync(cancellationToken);
        await _container.StartAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
