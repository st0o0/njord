
namespace Njord.Core.Domain;

public interface IFeatureRule
{
    FeatureDescriptor Descriptor { get; }
    bool Enabled { get; }
    PipelineEvent? Execute(RingBuffer<LocationForecast> ring, DateTimeOffset now);
}
