using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

var usePostgres = builder.Configuration.GetValue<bool>("UsePostgres");

var mosquitto = builder.AddContainer("mosquitto", "eclipse-mosquitto", "2")
    .WithEndpoint(targetPort: 1883, name: "mqtt", scheme: "tcp")
    .WithBindMount("mosquitto.conf", "/mosquitto/config/mosquitto.conf", isReadOnly: true)
    .WithPersistentLifetime();

var mqttEndpoint = mosquitto.GetEndpoint("mqtt");

builder.AddContainer("mqtt-explorer", "smeagolworms4/mqtt-explorer", "latest")
    .WithHttpEndpoint(targetPort: 4000, name: "http")
    .WithEnvironment("CONFIG_CONNECTIONS_0_NAME", "njord-local")
    .WithEnvironment("CONFIG_CONNECTIONS_0_HOST", mqttEndpoint.Property(EndpointProperty.Host))
    .WithEnvironment("CONFIG_CONNECTIONS_0_PORT", mqttEndpoint.Property(EndpointProperty.Port))
    .WaitFor(mosquitto);

var njord = builder.AddProject<Projects.Njord>("njord")
    .WithEnvironment("Njord__Mqtt__Host", mqttEndpoint.Property(EndpointProperty.Host))
    .WithEnvironment("Njord__Mqtt__Port", mqttEndpoint.Property(EndpointProperty.Port))
    .WaitFor(mosquitto);

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
