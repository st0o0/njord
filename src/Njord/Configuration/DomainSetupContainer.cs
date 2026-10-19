using Njord.AirQuality;
using Njord.Core.Configuration;
using Njord.Flood;
using Njord.Weather.Alerts;
using Servus.Application.Startup;

namespace Njord.Configuration;

public sealed class DomainSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddAlertOptions(configuration);
        services.AddWeatherDomain().AddAirQualityDomain().AddFloodDomain();
    }
}
