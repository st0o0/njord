using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Njord.Budget;
using Njord.Core.Configuration;
using Njord.Core.Health;
using Njord.Health;
using Njord.Pipeline;
using Njord.Weather;
using Prometheus;
using Servus.Core.Application.Startup;

namespace Njord.Configuration;

public sealed class CoreSetupContainer : IServiceSetupContainer
{
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
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<NjordOptions>, NjordOptionsValidator>();

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<NjordOptions>>().Value;
            return ParameterRegistry.Resolve(
                options.Parameters.Groups,
                options.Parameters.Extra,
                options.Parameters.Exclude);
        });

        services.AddSingleton(TimeProvider.System);

        services.AddSingleton(sp =>
        {
            var monitor = new NjordHealthMonitor
            {
                ServiceStartedUtc = sp.GetRequiredService<TimeProvider>().GetUtcNow(),
            };
            var budget = BudgetCalculator.GetEffectiveBudget(
                sp.GetRequiredService<IOptions<NjordOptions>>().Value);
            monitor.UpdateBudget(new BudgetHealthSnapshot(
                0, 0, RequestBudget.OpenMeteoFreeTierDailyLimit, budget.RequestsPerMonth));
            BudgetTelemetry.Observe(
                () => monitor.Budget.UsedDaily,
                () => monitor.Budget.UsedMonthly,
                () => monitor.Budget.LimitDaily,
                () => monitor.Budget.LimitMonthly);
            return monitor;
        });

        var mqttEnabled = configuration
            .GetSection(MqttOptions.SectionName)
            .Get<MqttOptions>()?.Enabled ?? false;

        var healthChecks = services.AddHealthChecks()
            .AddCheck<PipelineHealthCheck>("pipeline");
        if (mqttEnabled)
        {
            healthChecks.AddCheck<MqttConnectionHealthCheck>("mqtt-connection");
        }

        services.AddSingleton<IWritableOptions<NjordOptions>>(sp =>
        {
            var monitor = sp.GetRequiredService<IOptionsMonitor<NjordOptions>>();
            var configRoot = (IConfigurationRoot)sp.GetRequiredService<IConfiguration>();
            return new WritableNjordOptions(monitor, configRoot, Path.Combine("data", "appsettings.Override.json"));
        });
    }
}
