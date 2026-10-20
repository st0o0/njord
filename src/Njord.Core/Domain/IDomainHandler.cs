using Njord.Messages.Pipeline;
using Njord.Model;

namespace Njord.Core.Domain;

public interface IDomainHandler
{
    string ConfigKey { get; }

    IReadOnlySet<DomainKind> Triggers { get; }

    IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time);
}
