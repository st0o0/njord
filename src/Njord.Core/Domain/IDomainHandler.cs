using Njord.Messages.Pipeline;

namespace Njord.Core.Domain;

public interface IDomainHandler
{
    string ConfigKey { get; }

    DomainRequirements Requirements { get; }

    IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time);
}
