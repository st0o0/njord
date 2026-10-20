using Microsoft.Extensions.DependencyInjection;
using Njord.Core.Domain;

namespace Njord.Scores;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddScoreHandlers(this IServiceCollection services)
    {
        services.AddSingleton<IDomainHandler, BBQScoreHandler>();
        services.AddSingleton<IDomainHandler, LaundryScoreHandler>();
        services.AddSingleton<IDomainHandler, OutdoorScoreHandler>();
        services.AddSingleton<IDomainHandler, SurfConditionsHandler>();
        return services;
    }
}
