using Microsoft.Extensions.DependencyInjection;

namespace Njord.Alerts;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAlerts(this IServiceCollection services)
    {
        services.AddSingleton<AlertAnalyzer>();
        return services;
    }
}
