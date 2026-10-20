using Njord.Messages.Pipeline;

namespace Njord.Core.Domain;

public interface IFeatureRule
{
    FeatureDescriptor Descriptor { get; }
    bool Enabled { get; }
    IReadOnlyList<PipelineEvent> Execute(RingBuffer<LocationForecast> ring, DateTimeOffset now);
}
