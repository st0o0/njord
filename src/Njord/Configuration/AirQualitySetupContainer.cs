using Njord.AirQuality;
using Servus.Application.Startup;

namespace Njord.Configuration;

public sealed class AirQualitySetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddAirQualityAlerts();
    }
}
