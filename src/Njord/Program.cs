using Microsoft.AspNetCore.Server.Kestrel.Core;
using Njord.Configuration;
using Serilog;
using Servus.Core.Application.Startup;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog(config =>
{
    config
        .ReadFrom.Configuration(builder.Configuration)
        .Enrich.WithMachineName()
        .Enrich.WithThreadId()
        .Enrich.FromLogContext()
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
});
builder.Logging.ClearProviders();

var njordConfig = builder.Configuration.GetSection(NjordOptions.SectionName);
var grpcPort = njordConfig.GetValue("Grpc:Port", 8081);
var httpPort = njordConfig.GetValue("Http:Port", 8080);

builder.WebHost.ConfigureKestrel(options =>
{
    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
    {
        options.ListenAnyIP(httpPort, o => o.Protocols = HttpProtocols.Http1);
        options.ListenAnyIP(grpcPort, o => o.Protocols = HttpProtocols.Http2);
    }
    else
    {
        options.ConfigureEndpointDefaults(o => o.Protocols = HttpProtocols.Http1);
        options.ListenAnyIP(grpcPort, o => o.Protocols = HttpProtocols.Http2);
    }
});

builder.Configuration.AddJsonFile(
    Path.Combine("data", "njord-config.json"),
    optional: true,
    reloadOnChange: true);

var runner = AppBuilder.Create(builder, b => b.Build())
    .WithSetup<NjordServiceSetup>()
    .WithSetup<NjordActorSystemSetup>()
    .WithSetup<NjordApplicationSetup>()
    .Build();

await runner.RunAsync();