using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Compute.Analysis;
using Njord.Core.Configuration;

namespace Njord.Derived;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDerived(this IServiceCollection services)
    {
        services.AddSingleton<DerivedResultComputer>();
        services.AddSingleton(sp =>
        {
            var computer = sp.GetRequiredService<DerivedResultComputer>();
            var horizons = sp.GetRequiredService<IOptions<NjordOptions>>().Value.Horizons;
            return new DerivedAnalyzer(computer, [.. horizons]);
        });
        return services;
    }
}
