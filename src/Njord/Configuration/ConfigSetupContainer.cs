using Akka.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Persistence;
using Servus.Application.Startup;

namespace Njord.Configuration;

public sealed class ConfigSetupContainer : IServiceSetupContainer
{
    internal static readonly string OverridePath = Path.Combine("data", "appsettings.Override.json");

    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<NjordOptions>()
            .BindConfiguration(NjordOptions.SectionName)
            .ReplaceCollectionDefaults()
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<NjordOptions>, NjordOptionsValidator>();

        services
            .AddOptions<PersistenceOptions>()
            .BindConfiguration(PersistenceOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<PersistenceOptions>, PersistenceOptionsValidator>();

        services
            .AddOptions<StorageOptions>()
            .BindConfiguration(StorageOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<StorageOptions>, StorageOptionsValidator>();

        services.AddSingleton(static sp => sp.GetRequiredService<IOptions<NjordOptions>>().Value.Parameters.Resolve());

        services.AddSingleton<ConfigView>();
        // Program.cs registers the instance its configuration source reports to; this default only
        // serves hosts that build the configuration without the override source (tests).
        services.TryAddSingleton(static _ => new ConfigOverrideFile(OverridePath));
        services.AddSingleton<IConfigWriter>(static sp => new ConfigWriter(
            sp,
            (IConfigurationRoot)sp.GetRequiredService<IConfiguration>(),
            sp.GetRequiredService<ConfigOverrideFile>(),
            sp.GetRequiredService<TimeProvider>()));

        services.AddSingleton<IActorRegistration>(new ConfigActorRegistration());
    }

    private sealed class ConfigActorRegistration : IActorRegistration
    {
        public void Configure(AkkaConfigurationBuilder builder, IServiceProvider provider)
        {
            builder.WithActors((system, registry, resolver) =>
                registry.Register<IConfigManager>(system.ActorOf(resolver.Props<ConfigManager>(), "config-manager")));
        }
    }
}
