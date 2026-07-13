using Njord.Configuration;
using Servus.Core.Application.Startup;

var runner = AppBuilder.Create(WebApplication.CreateBuilder(args), b => b.Build())
    .WithSetup<NjordServiceSetup>()
    .WithSetup<NjordActorSystemSetup>()
    .WithSetup<NjordApplicationSetup>()
    .Build();

await runner.RunAsync();
