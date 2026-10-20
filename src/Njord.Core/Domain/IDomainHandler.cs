using Njord.Messages.Pipeline;

namespace Njord.Core.Domain;

public interface IDomainHandler
{
    FeatureDescriptor Feature { get; }

    IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time);
}
