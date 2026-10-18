using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Njord.Core.Configuration;
using Njord.Indices.Configuration;
using Njord.Indices.Scores;

namespace Njord.Indices;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIndices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddNjordOptions<IndexOptions>(configuration);

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
