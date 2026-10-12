using Microsoft.Extensions.DependencyInjection;
using Njord.Compute.Analysis;

namespace Njord.Derived;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDerived(this IServiceCollection services)
    {
        services.AddSingleton<DerivedResultComputer>();
        services.AddSingleton<DerivedAnalyzer>();
        return services;
    }
}
