using Serilog;
using Terp.Hosting;

namespace Njord.Configuration;

public sealed class LoggingSetup : ISetup<IServiceCollection>
{
    public void Setup(IServiceCollection services, SetupContext context)
    {
        services.AddSerilog(config =>
        {
            config
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.WithMachineName()
                .Enrich.WithThreadId()
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}");
        });
    }
}
