using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Persistence;
using Njord.Core.Configuration.Polling;
using Njord.Core.Domain;
using Servus.Application.Startup;

namespace Njord.Configuration;

public sealed class ConfigSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddNjordOptions<NjordOptions>(configuration);
        services.AddNjordOptions<FeatureOptions>(configuration);
        services.AddNjordOptions<PersistenceOptions>(configuration);

        services.AddSingleton<ConfigView>();
        services.AddSingleton(static sp => ServiceInfoMappings.ToServiceInfo(
            sp.GetRequiredService<IOptions<NjordOptions>>().Value,
            sp.GetRequiredService<IOptions<PollingOptions>>().Value,
            sp.GetRequiredService<IOptions<AlertOptions>>().Value,
            sp.GetServices<IFeatureRule>()
                .Where(static rule => rule.Enabled)
                .Select(static rule => (IFeature)new RuleFeature(rule))
                .ToList()));
    }

    private sealed class RuleFeature(IFeatureRule rule) : IFeature
    {
        public string Name { get; } = rule.GetType().Name;

        public FeatureDescriptor Feature { get; } = rule.Descriptor;
    }
}
