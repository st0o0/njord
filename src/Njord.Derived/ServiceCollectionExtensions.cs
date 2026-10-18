using Microsoft.Extensions.DependencyInjection;
using Njord.Consensus;

namespace Njord.Derived;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDerived(this IServiceCollection services)
    {
        services.AddSingleton<DerivedAnalyzer>();
        return services;
    }
}
