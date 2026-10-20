using Njord.Messages.Pipeline;

namespace Njord.Core.Domain;

public interface IDomainHandler : IFeature
{
    IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time);
}
