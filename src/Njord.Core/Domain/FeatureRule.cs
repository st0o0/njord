
namespace Njord.Core.Domain;

public abstract class FeatureRule<TInput> : IFeatureRule where TInput : struct
{
    public abstract FeatureDescriptor Descriptor { get; }
    public abstract bool Enabled { get; }

    protected abstract TInput? Project(LocationForecast forecast);
    protected abstract PipelineEvent? Evaluate(FeatureContext<TInput> ctx);

    public PipelineEvent? Execute(RingBuffer<LocationForecast> ring, DateTimeOffset now)
    {
        var depth = Descriptor.PollDepth;
        var history = new List<TInput>(depth + 1);
        var count = Math.Min(ring.Count, depth + 1);

        for (var i = 0; i < count; i++)
        {
            if (Project(ring[i]) is { } input)
            {
                history.Add(input);
            }
        }

        if (history.Count == 0)
        {
            return null;
        }

        var ctx = new FeatureContext<TInput>(ring[0].Location, history[0], history, now);
        return Evaluate(ctx);
    }
}
