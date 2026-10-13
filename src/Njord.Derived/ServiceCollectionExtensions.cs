using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Compute.Consensus;
using Njord.Compute.Contracts;
using Njord.Core.Configuration.Derived;

namespace Njord.Derived;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDerived(this IServiceCollection services)
    {
        services
            .AddOptions<DerivedOptions>()
            .BindConfiguration(DerivedOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<DerivedOptions>, DerivedOptionsValidator>();

        services.AddSingleton<DerivedAnalyzer>();
        return services;
    }
}
