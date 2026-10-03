using Akka.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Njord.Actors;
using Njord.Messages.Pipeline;

namespace Njord.Pipeline;

public static class PipelineServiceCollectionExtensions
{
    public static IServiceCollection AddNjordPipeline(this IServiceCollection services)
    {
        services.AddSingleton<IBudgetProvider, OptionsBudgetProvider>();
        services.AddSingleton<IBudgetGate<WeightedTarget>>(sp =>
            new WeightedBudgetGate(
                sp.GetRequiredService<IBudgetProvider>(),
                sp.GetRequiredService<ActorRegistry>().Get<IBudgetTrackerActor>(),
                sp.GetRequiredService<TimeProvider>()));
        return services;
    }
}
