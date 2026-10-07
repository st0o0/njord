using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Health;
using Njord.Core.Diagnostics;
using Njord.Domain.Weather;
using Njord.Health;
using Prometheus;
using Servus.Core.Application.Startup;

namespace Njord.Configuration;

public sealed class CoreSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        Metrics.ConfigureMeterAdapter(options =>
        {
            options.InstrumentFilterPredicate = instrument => instrument.Meter.Name == "Njord";
        });

        services
            .AddOptions<NjordOptions>()
            .Bind(configuration.GetSection(NjordOptions.SectionName))
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
            var state = new NjordHealthState
            {
                ServiceStartedUtc = sp.GetRequiredService<TimeProvider>().GetUtcNow(),
            };
            var budget = BudgetCalculator.GetEffectiveBudget(
                sp.GetRequiredService<IOptions<NjordOptions>>().Value);
            state.SetBudgetLimits(RequestBudget.OpenMeteoFreeTierDailyLimit, budget.RequestsPerMonth);
            NjordMetrics.Instance.AddBudgetUsedDaily(() => state.BudgetUsedDaily);
            NjordMetrics.Instance.AddBudgetUsedMonthly(() => state.BudgetUsedMonthly);
            NjordMetrics.Instance.AddBudgetLimitDaily(() => state.BudgetLimitDaily);
            NjordMetrics.Instance.AddBudgetLimitMonthly(() => state.BudgetLimitMonthly);
            return state;
        });

        var mqttEnabled = configuration
            .GetSection($"{NjordOptions.SectionName}:Mqtt")
            .GetValue("Enabled", false);

        var healthChecks = services.AddHealthChecks()
            .AddCheck<PipelineHealthCheck>("pipeline");
        if (mqttEnabled)
        {
            healthChecks.AddCheck<MqttConnectionHealthCheck>("mqtt-connection");
        }

        services.AddSingleton<ConfigPersistence>();
    }
}
