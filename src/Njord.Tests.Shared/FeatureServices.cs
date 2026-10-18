using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Njord.Alerts;
using Njord.Alerts.Configuration;
using Njord.Indices;
using Njord.Indices.Configuration;

namespace Njord.Tests.Shared;

public static class FeatureServices
{
    public static ServiceProvider Build(TimeProvider time, Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(AppSettingsDefaults.Configuration());
        services.AddSingleton(time);
        configure(services);
        return services.BuildServiceProvider();
    }

    public static AlertAnalyzer Alerts(TimeProvider time, bool enabled = true) =>
        Build(time, services => services
                .AddAlerts(AppSettingsDefaults.Configuration())
                .Configure<AlertOptions>(options => options.Enabled = enabled))
            .GetRequiredService<AlertAnalyzer>();

    public static IndexAnalyzer Indices(TimeProvider time, bool enabled = true) =>
        Build(time, services => services
                .AddIndices(AppSettingsDefaults.Configuration())
                .Configure<IndexOptions>(options => options.Enabled = enabled))
            .GetRequiredService<IndexAnalyzer>();
}
