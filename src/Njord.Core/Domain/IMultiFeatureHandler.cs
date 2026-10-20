namespace Njord.Core.Domain;

// A handler that serves several independently toggled features (the weather alert handler runs one rule per feature).
// Its Feature is the composite of the enabled features; Features lists every feature it can serve.
public interface IMultiFeatureHandler : IDomainHandler
{
    IReadOnlyList<FeatureDescriptor> Features { get; }
}
