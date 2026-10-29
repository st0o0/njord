using Njord.Core.Domain;
using Njord.Messages.Stream;


namespace Njord.Messages.Results;

public interface IFeatureRule
{
    FeatureDescriptor Descriptor { get; }
    bool Enabled { get; }
    PipelineEvent? Execute(RingBuffer<LocationForecast> ring, DateTimeOffset now);

    IReadOnlyList<PipelineEvent> ExecuteAll(RingBuffer<LocationForecast> ring, DateTimeOffset now) =>
        Execute(ring, now) is { } result ? [result] : [];
}
