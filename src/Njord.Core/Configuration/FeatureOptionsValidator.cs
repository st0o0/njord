using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Core.Domain;

namespace Njord.Core.Configuration;

// Resolves the handlers lazily: they depend on IOptions<FeatureOptions>, so injecting them would be circular.
public sealed class FeatureOptionsValidator(IServiceProvider services) : IValidateOptions<FeatureOptions>
{
    public ValidateOptionsResult Validate(string? name, FeatureOptions options)
    {
        var features = FeatureResolution.AllFeatures(services.GetServices<IDomainHandler>());
        var known = features.Select(feature => feature.SectionKey()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var unregistered = options.ConfiguredKeys().Where(key => !known.Contains(key)).Order().ToList();
        if (unregistered.Count > 0)
        {
            return ValidateOptionsResult.Fail($"No feature is registered for: {string.Join(", ", unregistered)}.");
        }

        return features.Any(options.IsEnabled)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("At least one feature must be enabled.");
    }
}
