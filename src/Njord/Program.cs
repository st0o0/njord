using Akka.Hosting;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain;
using Njord.Egress;
using Njord.Ingest;
using Njord.Pipeline;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<NjordOptions>()
    .Bind(builder.Configuration.GetSection(NjordOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<NjordOptions>, NjordOptionsValidator>();
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<NjordOptions>>().Value;
    return ParameterRegistry.Resolve(
        options.Parameters.Groups,
        options.Parameters.Extra,
        options.Parameters.Exclude);
});
builder.Services.AddOpenMeteoIngest();
builder.Services.AddMqttEgress();
builder.Services.AddHealthChecks();

builder.Services.AddAkka("njord", (akka, _) =>
{
    akka.WithActors((system, registry, resolver) =>
    {
        registry.Register<MqttConnectionActor>(
            system.ActorOf(resolver.Props<MqttConnectionActor>(), "mqtt-egress"));
        registry.Register<PipelineGuardianActor>(
            system.ActorOf(resolver.Props<PipelineGuardianActor>(), "poll-pipeline"));
    });
});

var app = builder.Build();
app.MapHealthChecks("/healthz");
await app.RunAsync();
