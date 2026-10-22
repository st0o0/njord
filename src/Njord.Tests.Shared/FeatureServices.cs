using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Njord.Core.Domain;
using Njord.Features;

namespace Njord.Tests.Shared;

public static class FeatureServices
{
    public static ServiceProvider Build(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        var configuration = AppSettingsDefaults.Configuration();
        services.AddSingleton<IConfiguration>(configuration);
        configure(services);
        return services.BuildServiceProvider();
    }

    public static ServiceProvider BuildAllFeatures(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        var configuration = AppSettingsDefaults.Configuration();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddFeatures();
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    public static IReadOnlyList<IFeatureRule> AllFeatureRules(Action<IServiceCollection>? configure = null) =>
        [.. BuildAllFeatures(configure).GetServices<IFeatureRule>()];

    public static T GetFeature<T>(Action<IServiceCollection>? configure = null) where T : IFeatureRule =>
        BuildAllFeatures(configure).GetServices<IFeatureRule>().OfType<T>().Single();
}
