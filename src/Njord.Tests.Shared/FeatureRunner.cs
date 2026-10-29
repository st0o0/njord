using Njord.Core.Domain;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Tests.Shared;

public static class FeatureRunner
{
    // Pushes the forecasts oldest to newest, as the pipeline does, and runs Project + Evaluate.
    public static PipelineEvent? Run<TInput>(
        FeatureRule<TInput> rule, DateTimeOffset now, params LocationForecast[] oldestToNewest)
        where TInput : struct =>
        rule.Execute(Ring(rule, oldestToNewest), now);

    public static IReadOnlyList<PipelineEvent> RunAll<TInput>(
        FeatureRule<TInput> rule, DateTimeOffset now, params LocationForecast[] oldestToNewest)
        where TInput : struct =>
        rule.ExecuteAll(Ring(rule, oldestToNewest), now);

    private static RingBuffer<LocationForecast> Ring<TInput>(FeatureRule<TInput> rule, LocationForecast[] oldestToNewest)
        where TInput : struct
    {
        var ring = new RingBuffer<LocationForecast>(Math.Max(oldestToNewest.Length, rule.Descriptor.PollDepth + 1));
        foreach (var forecast in oldestToNewest)
        {
            ring.Push(forecast);
        }

        return ring;
    }
}
