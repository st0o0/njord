using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration.Indices;
using Njord.Indices.Scores;

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

        services.AddSingleton<IIndexScore, LaundryScore>();
        services.AddSingleton<IIndexScore, OutdoorScore>();
        services.AddSingleton<IIndexScore, RunningScore>();
        services.AddSingleton<IIndexScore, CyclingScore>();
        services.AddSingleton<IIndexScore, BbqScore>();
        services.AddSingleton<IIndexScore, IrrigationScore>();
        services.AddSingleton<IIndexScore, SolarScore>();
        services.AddSingleton<IIndexScore, NightVentilationScore>();

        services.AddSingleton<IndexAnalyzer>();
        return services;
    }
}
