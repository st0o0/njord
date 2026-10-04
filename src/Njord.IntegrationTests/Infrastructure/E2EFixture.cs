using Akka.Actor;
using Akka.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Njord.Ingest;
using Njord.Tests.Shared;

namespace Njord.IntegrationTests.Infrastructure;

public sealed class E2EFixture : IAsyncLifetime
{
    private readonly MosquittoFixture _mosquitto = new();
    private WebApplicationFactory<Program>? _factory;

    public FakeTimeProvider TimeProvider { get; } = new(
        new DateTimeOffset(2026, 7, 12, 6, 0, 0, TimeSpan.Zero));

    public FakeOpenMeteoClient OpenMeteoClient { get; } = new();
    public MosquittoFixture Mosquitto => _mosquitto;
    public HttpClient Client { get; private set; } = null!;
    public ActorSystem System { get; private set; } = null!;
    public IActorRegistry Registry { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _mosquitto.InitializeAsync();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Njord:Locations:0:Name", "lucerne");
                builder.UseSetting("Njord:Locations:0:Latitude", "47.05");
                builder.UseSetting("Njord:Locations:0:Longitude", "8.31");
                builder.UseSetting("Njord:Models:0", "icon_d2");
                builder.UseSetting("Njord:PollIntervalMinutes", "60");
                builder.UseSetting("Njord:Mqtt:Enabled", "true");
                builder.UseSetting("Njord:Mqtt:Host", "localhost");
                builder.UseSetting("Njord:Mqtt:Port", _mosquitto.MqttPort.ToString());
                builder.UseSetting("Njord:PersistencePath",
                    Path.Combine(Path.GetTempPath(), $"njord-e2e-{Guid.NewGuid():N}", "journal.db"));

                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<TimeProvider>(TimeProvider);
                    services.AddSingleton<IOpenMeteoClient>(OpenMeteoClient);
                });
            });

        Client = _factory.CreateClient();

        using var warmUp = await Client.GetAsync("/alive");

        System = _factory.Services.GetRequiredService<ActorSystem>();
        Registry = _factory.Services.GetRequiredService<IActorRegistry>();
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _mosquitto.DisposeAsync();
    }
}
