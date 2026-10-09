using Microsoft.Extensions.DependencyInjection;

namespace Njord.History;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHistory(this IServiceCollection services)
    {
        services.AddSingleton<HistoryComputer>();
        services.AddSingleton<HistoryTracker>();
        return services;
    }
}
