using Microsoft.AspNetCore.Server.Kestrel.Core;
using Terp.Hosting;

namespace Njord.Configuration;

public sealed class CoreSetup : ISetup<IServiceCollection>
{
    public void Setup(IServiceCollection services, SetupContext context)
    {
        services.AddSingleton(TimeProvider.System);
        services.Configure<KestrelServerOptions>(options => options.Limits.MinResponseDataRate = null);
    }
}
