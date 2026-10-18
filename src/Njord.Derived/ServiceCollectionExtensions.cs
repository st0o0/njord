using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Njord.Consensus;
using Njord.Core.Configuration;
using Njord.Derived.Configuration;

namespace Njord.Derived;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDerived(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddNjordOptions<DerivedOptions>(configuration);

        services.AddSingleton<DerivedAnalyzer>();
        return services;
    }
}
