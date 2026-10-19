using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;

namespace Njord.AirQuality;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAirQualityAlerts(this IServiceCollection services)
    {
        services.AddSingleton<IDomainHandler>(static sp =>
        {
            var options = sp.GetRequiredService<IOptions<AlertOptions>>().Value;
            return new AirQualityAlertHandler([new PollenAlertRule(), new Pm25AlertRule()], options.ToSettings(), options.Enabled);
        });
        return services;
    }
}
