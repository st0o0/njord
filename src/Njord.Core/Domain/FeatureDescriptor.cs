using Njord.Messages.Pipeline;

namespace Njord.Core.Domain;

// Requirements carries both the required and the optional parameters (DomainRequirements.Optional*).
public sealed record FeatureDescriptor(
    OutputKind Output,
    FeatureCategory Category,
    string Name,
    bool DefaultEnabled,
    DomainRequirements Requirements,
    int PollDepth = 0);
