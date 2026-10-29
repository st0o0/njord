
namespace Njord.Core.Domain;

public sealed record FeatureDescriptor(
    OutputKind Output,
    FeatureCategory Category,
    string Name,
    DomainRequirements Requirements,
    int PollDepth = 0);
