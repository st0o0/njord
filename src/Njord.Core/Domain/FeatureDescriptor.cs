
namespace Njord.Core.Domain;

public sealed record FeatureDescriptor(
    OutputKind Output,
    FeatureCategory Category,
    string Name,
    bool DefaultEnabled,
    DomainRequirements Requirements,
    int PollDepth = 0);
