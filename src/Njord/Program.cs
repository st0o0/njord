using Njord.Configuration;
using Njord.ServiceDefaults;
using Servus.Core.Application.Startup;

var runner = AppBuilder.Create(WebApplication.CreateBuilder(args).AddNjordTelemetry(), b => b.Build())
    .WithSetup<NjordServiceSetup>()
    .WithSetup<NjordActorSystemSetup>()
    .WithSetup<NjordApplicationSetup>()
    .Build();

await runner.RunAsync();