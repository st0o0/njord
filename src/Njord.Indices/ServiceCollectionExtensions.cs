using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Compute.Analysis;
using Njord.Core.Configuration.Indices;

namespace Njord.Indices;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIndices(this IServiceCollection services)
    {
        services
            .AddOptions<IndexOptions>()
            .BindConfiguration(IndexOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<IndexOptions>, IndexOptionsValidator>();

        services.AddSingleton<IndexComputer>();
        services.AddSingleton<IndexAnalyzer>();
        return services;
    }
}
