using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Core.Domain;

namespace Njord.Core.Configuration;

// Resolves the rules lazily: they depend on their own IOptions, so injecting them would be circular.
public sealed class FeatureOptionsValidator(IServiceProvider services) : IValidateOptions<FeatureOptions>
{
    public ValidateOptionsResult Validate(string? name, FeatureOptions options) =>
        services.GetServices<IFeatureRule>().Any(rule => rule.Enabled)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("At least one feature must be enabled.");
}
