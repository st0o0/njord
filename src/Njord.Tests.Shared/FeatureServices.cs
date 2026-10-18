using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Njord.Alerts;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Alerts;
using Njord.Core.Configuration.Derived;
using Njord.Core.Configuration.Indices;
using Njord.Core.Configuration.Trends;
using Njord.Indices;

namespace Njord.Tests.Shared;

public static class FeatureServices
{
    public static ServiceProvider Build(TimeProvider time, Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(AppSettingsDefaults.Configuration());
        services.AddSingleton(time);
        var configuration = AppSettingsDefaults.Configuration();
        services.AddNjordOptions<AlertOptions>(configuration);
        services.AddNjordOptions<DerivedOptions>(configuration);
        services.AddNjordOptions<TrendOptions>(configuration);
        services.AddNjordOptions<IndexOptions>(configuration);
        configure(services);
        return services.BuildServiceProvider();
    }

    public static AlertAnalyzer Alerts(TimeProvider time, bool enabled = true) =>
        Build(time, services => services
                .AddAlerts()
                .Configure<AlertOptions>(options => options.Enabled = enabled))
            .GetRequiredService<AlertAnalyzer>();

    public static IndexAnalyzer Indices(TimeProvider time, bool enabled = true) =>
        Build(time, services => services
                .AddIndices()
                .Configure<IndexOptions>(options => options.Enabled = enabled))
            .GetRequiredService<IndexAnalyzer>();
}
