using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.Features.Alerts.Uv;

public sealed class UvConfigValidator : IValidateOptions<UvConfig>
{
    public ValidateOptionsResult Validate(string? name, UvConfig options)
    {
        var path = $"{IConfigSection.SectionOf<UvConfig>()}:Thresholds";
        var values = options.Thresholds;
        if (values.Length != 4)
        {
            return ValidateOptionsResult.Fail($"{path} needs exactly four values (moderate, high, very high, extreme), got {values.Length}.");
        }

        if (!values.All(double.IsFinite))
        {
            return ValidateOptionsResult.Fail($"{path} must contain only finite numbers, got [{string.Join(", ", values)}].");
        }

        return values[0] < values[1] && values[1] < values[2] && values[2] < values[3]
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"{path} must be strictly ascending (moderate, high, very high, extreme), got [{string.Join(", ", values)}].");
    }
}
