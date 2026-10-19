using Njord.Flood;
using Servus.Application.Startup;

namespace Njord.Configuration;

public sealed class FloodSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddFloodAlerts();
    }
}
