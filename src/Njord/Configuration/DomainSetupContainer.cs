using Microsoft.Extensions.Options;
using Njord.AirQuality;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Flood;
using Njord.Forecast;
using Njord.Marine;
using Njord.Weather.Alerts;
using Servus.Application.Startup;

namespace Njord.Configuration;

public sealed class DomainSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddAlertOptions(configuration);
        services.AddWeatherDomain().AddAirQualityDomain().AddFloodDomain().AddMarineDomain();

        services.AddSingleton<IReadOnlySet<DomainKind>>(static sp => EnrichmentResolution.RequiredDomains(
            sp.GetRequiredService<IOptions<EnrichmentOptions>>().Value,
            sp.GetServices<IDomainHandler>()));
    }
}
