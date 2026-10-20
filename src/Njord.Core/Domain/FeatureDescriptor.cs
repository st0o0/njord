namespace Njord.Core.Domain;

// Requirements carries both the required and the optional parameters (DomainRequirements.Optional*).
public sealed record FeatureDescriptor(
    FeatureKind Kind,
    FeatureCategory Category,
    string Description,
    bool DefaultEnabled,
    DomainRequirements Requirements);
