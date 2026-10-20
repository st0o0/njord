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
        var known = features.Select(feature => feature.Kind).ToHashSet();

        var unregistered = options.Keys.Where(kind => !known.Contains(kind)).Order().ToList();
        if (unregistered.Count > 0)
        {
            return ValidateOptionsResult.Fail(
                $"No feature is registered for: {string.Join(", ", unregistered.Select(kind => kind.ToConfigKey()))}.");
        }

        return features.Any(options.IsEnabled)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("At least one feature must be enabled.");
    }
}
