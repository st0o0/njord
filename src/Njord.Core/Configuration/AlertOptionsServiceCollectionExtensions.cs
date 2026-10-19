using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Njord.Core.Configuration;

public static class AlertOptionsServiceCollectionExtensions
{
    public static IServiceCollection AddAlertOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddNjordOptions<AlertOptions>(configuration);
        return services;
    }
}
