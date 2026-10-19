using Njord.Alerts;
using Njord.Weather.Alerts;
using Servus.Application.Startup;

namespace Njord.Configuration;

public sealed class WeatherAlertSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddAlertOptions(configuration);
        services.AddWeatherAlerts();
    }
}
