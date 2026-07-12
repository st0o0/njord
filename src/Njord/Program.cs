using Akka.Hosting;
using Akka.Persistence.Sqlite;
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
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddOpenMeteoIngest();
builder.Services.AddMqttEgress();
builder.Services.AddHealthChecks();

builder.Services.AddAkka("njord", (akka, sp) =>
{
    var njordOptions = sp.GetRequiredService<IOptions<NjordOptions>>().Value;
    var persistencePath = njordOptions.PersistencePath;

    akka
        .AddHocon(SqlitePersistence.DefaultConfiguration(), HoconAddMode.Prepend)
        .AddHocon($$"""
            akka.persistence {
                journal {
                    plugin = "akka.persistence.journal.sqlite"
                    sqlite {
                        connection-string = "Data Source={{persistencePath}}"
                        auto-initialize = true
                    }
                }
                snapshot-store {
                    plugin = "akka.persistence.snapshot-store.sqlite"
                    sqlite {
                        connection-string = "Data Source={{persistencePath}}"
                        auto-initialize = true
                    }
                }
            }
            """, HoconAddMode.Prepend)
        .WithActors((system, registry, resolver) =>
        {
            registry.Register<MqttEgressActor>(
                system.ActorOf(resolver.Props<MqttEgressActor>(), "mqtt-egress"));
            registry.Register<PipelineActor>(
                system.ActorOf(resolver.Props<PipelineActor>(), "pipeline"));
            registry.Register<SchedulerActor>(
                system.ActorOf(resolver.Props<SchedulerActor>(), "scheduler"));
        });
});

var app = builder.Build();
app.MapHealthChecks("/healthz");
await app.RunAsync();
