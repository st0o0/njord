using Njord.Messages.Pipeline;

namespace Njord.Core.Domain;

// Requirements carries both the required and the optional parameters (DomainRequirements.Optional*).
public sealed record FeatureDescriptor(
    OutputKind Output,
    FeatureCategory Category,
    bool DefaultEnabled,
    DomainRequirements Requirements);
