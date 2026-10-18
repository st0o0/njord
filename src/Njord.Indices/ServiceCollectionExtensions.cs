using Microsoft.Extensions.DependencyInjection;
using Njord.Indices.Scores;

namespace Njord.Indices;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIndices(this IServiceCollection services)
    {
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
