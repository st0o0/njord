using Njord.Configuration;

var builder = WebApplication.CreateBuilder(args);

new NjordServiceSetup().SetupServices(builder.Services, builder.Configuration);
new NjordActorSystemSetup().SetupServices(builder.Services, builder.Configuration);
builder.Services.AddHealthChecks();

var app = builder.Build();
app.MapHealthChecks("/healthz");
await app.RunAsync();
