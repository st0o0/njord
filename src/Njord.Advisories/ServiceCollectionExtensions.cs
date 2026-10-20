using Microsoft.Extensions.DependencyInjection;
using Njord.Core.Domain;

namespace Njord.Advisories;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAdvisoryHandlers(this IServiceCollection services)
    {
        services.AddSingleton<IDomainHandler, CommuteSafetyHandler>();
        services.AddSingleton<IDomainHandler, VentilationWindowHandler>();
        services.AddSingleton<IDomainHandler, NightVentilationHandler>();
        services.AddSingleton<IDomainHandler, ExerciseAdvisoryHandler>();
        services.AddSingleton<IDomainHandler, GardeningHandler>();
        services.AddSingleton<IDomainHandler, FloodTimelineHandler>();
        return services;
    }
}
