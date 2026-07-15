using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;

static int FindFreePort()
{
    using var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    return port;
}

var builder = DistributedApplication.CreateBuilder(args);

var usePostgres = builder.Configuration.GetValue<bool>("UsePostgres");

var mosquitto = builder.AddContainer("mosquitto", "eclipse-mosquitto", "2")
    .WithEndpoint(targetPort: 1883, name: "mqtt", scheme: "tcp")
    .WithBindMount("mosquitto.conf", "/mosquitto/config/mosquitto.conf", isReadOnly: true)
    .WithPersistentLifetime();

var mqttEndpoint = mosquitto.GetEndpoint("mqtt");

var wireMock = builder.AddContainer("wiremock", "sheyenrath/wiremock.net-alpine", "latest")
    .WithHttpEndpoint(targetPort: 80, name: "http");

var wireMockEndpoint = wireMock.GetEndpoint("http");

var grpcPort = FindFreePort();
var njord = builder.AddProject<Projects.Njord>("njord")
    .WithHttpEndpoint(name: "http")
    .WithEndpoint("grpc", e =>
    {
        e.IsProxied = false;
        e.Port = grpcPort;
        e.UriScheme = "http";
    })
    .WithEnvironment("Njord__Grpc__Port", grpcPort.ToString())
    .WithEnvironment("Njord__Mqtt__Host", mqttEndpoint.Property(EndpointProperty.Host))
    .WithEnvironment("Njord__Mqtt__Port", mqttEndpoint.Property(EndpointProperty.Port))
    .WithEnvironment("Njord__OpenMeteoBaseUrl", wireMockEndpoint)
    .WithEnvironment("Njord__PollInterval", "01:00:00")
    .WithEnvironment("Njord__PersistencePath", Path.Combine(Path.GetTempPath(), $"njord-test-{Guid.NewGuid():N}.db"))
    .WaitFor(mosquitto)
    .WaitFor(wireMock);

if (usePostgres)
{
    var postgres = builder.AddPostgres("pg")
        .WithPersistentLifetime()
        .AddDatabase("njord-db");

    njord
        .WithEnvironment("Njord__Persistence__Provider", "PostgreSql")
        .WithEnvironment("Njord__Persistence__ConnectionString", postgres.Resource.ConnectionStringExpression)
        .WaitFor(postgres);
}

await builder.Build().RunAsync();
