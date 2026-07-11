using Akka.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Ingest;
using Njord.Pipeline;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOptions<NjordOptions>()
    .Bind(builder.Configuration.GetSection(NjordOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<NjordOptions>, NjordOptionsValidator>();
builder.Services.AddOpenMeteoIngest();

builder.Services.AddAkka("njord", (akka, _) =>
{
    akka.WithActors((system, registry, resolver) =>
        registry.Register<PipelineGuardianActor>(
            system.ActorOf(resolver.Props<PipelineGuardianActor>(), "poll-pipeline")));
});

await builder.Build().RunAsync();
