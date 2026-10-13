using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Alerts;

namespace Njord.Alerts;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAlerts(this IServiceCollection services)
    {
        services
            .AddOptions<AlertOptions>()
            .BindConfiguration(AlertOptions.SectionName)
            .ReplaceCollectionDefaults()
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<AlertOptions>, AlertOptionsValidator>();

        services.AddSingleton<AlertAnalyzer>();
        return services;
    }
}
