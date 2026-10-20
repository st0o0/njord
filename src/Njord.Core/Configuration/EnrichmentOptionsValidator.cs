using Microsoft.Extensions.Options;

namespace Njord.Core.Configuration;

public sealed class EnrichmentOptionsValidator : IValidateOptions<EnrichmentOptions>
{
    public ValidateOptionsResult Validate(string? name, EnrichmentOptions options) =>
        options.EnabledKeys().Count == 0
            ? ValidateOptionsResult.Fail("At least one enrichment must be enabled.")
            : ValidateOptionsResult.Success;
}
