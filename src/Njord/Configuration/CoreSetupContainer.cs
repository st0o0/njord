using Microsoft.AspNetCore.Server.Kestrel.Core;
using Servus.Application.Startup;

namespace Njord.Configuration;

public sealed class CoreSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.Configure<KestrelServerOptions>(options => options.Limits.MinResponseDataRate = null);
    }
}
