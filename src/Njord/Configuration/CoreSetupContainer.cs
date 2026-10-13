using Akka.Hosting;
using Microsoft.Extensions.Options;
using Njord.Budget;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Mqtt;
using Njord.Core.Configuration.Persistence;
using Njord.Core.Configuration.Polling;
using Njord.Core.Health;
using Njord.Health;
using Njord.Weather;
using Prometheus;
using Servus.Application.Startup;

namespace Njord.Configuration;

public sealed class CoreSetupContainer : IServiceSetupContainer
{
    internal static readonly string OverridePath = Path.Combine("data", "appsettings.Override.json");

    private static readonly HashSet<string> NjordMeters =
        ["Njord.OpenMeteo", "Njord.Budget", "Njord.Pipeline", "Njord.Mqtt"];

    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        Metrics.ConfigureMeterAdapter(options =>
        {
            options.InstrumentFilterPredicate = instrument => NjordMeters.Contains(instrument.Meter.Name);
        });

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

        services.AddSingleton(static sp => ResolveParameters(sp.GetRequiredService<IOptions<NjordOptions>>()));

        services.AddSingleton(TimeProvider.System);

        services.AddSingleton(static sp => CreateHealthMonitor(
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<IOptions<PollingOptions>>()));

        var mqttEnabled = configuration
            .GetSection(MqttOptions.SectionName)
            .Get<MqttOptions>()?.Enabled ?? false;

        var healthChecks = services.AddHealthChecks()
            .AddCheck<PipelineHealthCheck>("pipeline");
        if (mqttEnabled)
        {
            healthChecks.AddCheck<MqttConnectionHealthCheck>("mqtt-connection");
        }

        services.AddSingleton<ConfigView>();
        services.AddSingleton<IConfigWriter>(static sp => new ConfigWriter(
            sp,
            (IConfigurationRoot)sp.GetRequiredService<IConfiguration>(),
            OverridePath));

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

    private static ResolvedParameterSet ResolveParameters(IOptions<NjordOptions> options)
    {
        var parameters = options.Value.Parameters;
        return ParameterRegistry.Resolve(parameters.Groups, parameters.Extra, parameters.Exclude);
    }

    private static NjordHealthMonitor CreateHealthMonitor(TimeProvider timeProvider, IOptions<PollingOptions> polling)
    {
        var monitor = new NjordHealthMonitor
        {
            ServiceStartedUtc = timeProvider.GetUtcNow(),
        };
        var budget = BudgetCalculator.GetEffectiveBudget(polling.Value);
        monitor.UpdateBudget(new BudgetHealthSnapshot(
            0, 0, RequestBudget.OpenMeteoFreeTierDailyLimit, budget.RequestsPerMonth));
        BudgetTelemetry.Observe(
            () => monitor.Budget.UsedDaily,
            () => monitor.Budget.UsedMonthly,
            () => monitor.Budget.LimitDaily,
            () => monitor.Budget.LimitMonthly);
        return monitor;
    }
}
