using Njord.Core.Domain;
using Njord.Messages.Stream;


namespace Njord.Messages.Results;

public abstract class FeatureRule<TInput> : IFeatureRule where TInput : struct
{
    public abstract FeatureDescriptor Descriptor { get; }
    public abstract bool Enabled { get; }

    protected abstract TInput? Project(LocationForecast forecast);
    protected abstract PipelineEvent? Evaluate(FeatureContext<TInput> ctx);

    protected virtual IReadOnlyList<PipelineEvent> EvaluateAll(FeatureContext<TInput> ctx) =>
        Evaluate(ctx) is { } result ? [result] : [];

    public PipelineEvent? Execute(RingBuffer<LocationForecast> ring, DateTimeOffset now) =>
        BuildContext(ring, now) is { } ctx ? Evaluate(ctx) : null;

    public IReadOnlyList<PipelineEvent> ExecuteAll(RingBuffer<LocationForecast> ring, DateTimeOffset now) =>
        BuildContext(ring, now) is { } ctx ? EvaluateAll(ctx) : [];

    private FeatureContext<TInput>? BuildContext(RingBuffer<LocationForecast> ring, DateTimeOffset now)
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

        return history.Count == 0 ? null : new FeatureContext<TInput>(ring[0].Location, history[0], history, now);
    }
}
