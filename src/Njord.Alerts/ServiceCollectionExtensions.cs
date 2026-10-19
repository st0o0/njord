using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Njord.Alerts.Configuration;
using Njord.Core.Configuration;

namespace Njord.Alerts;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAlertOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddNjordOptions<AlertOptions>(configuration);
        return services;
    }
}
