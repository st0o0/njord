using Njord.Core.Domain;
using Njord.Messages.Pipeline;

namespace Njord.Tests.Shared;

public static class FeatureRunner
{
    // Pushes the forecasts oldest to newest, as the pipeline does, and runs Project + Evaluate.
    public static PipelineEvent? Run<TInput>(
        FeatureRule<TInput> rule, DateTimeOffset now, params LocationForecast[] oldestToNewest)
        where TInput : struct
    {
        var ring = new RingBuffer<LocationForecast>(Math.Max(oldestToNewest.Length, rule.Descriptor.PollDepth + 1));
        foreach (var forecast in oldestToNewest)
        {
            ring.Push(forecast);
        }

        return rule.Execute(ring, now);
    }
}
