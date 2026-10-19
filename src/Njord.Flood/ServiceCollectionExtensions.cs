using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;

namespace Njord.Flood;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFloodAlerts(this IServiceCollection services)
    {
        services.AddSingleton<IDomainHandler>(static sp =>
        {
            var options = sp.GetRequiredService<IOptions<AlertOptions>>().Value;
            return new FloodRiskHandler(new FloodRiskAlertRule(), options.ToSettings(), options.Enabled);
        });

        services.AddSingleton<IDomainHandler>(static sp =>
        {
            var options = sp.GetRequiredService<IOptions<AlertOptions>>().Value;
            return new FloodRainHandler(new FloodRainAlertRule(), options.ToSettings(), options.Enabled);
        });
        return services;
    }
}
