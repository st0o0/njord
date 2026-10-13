using Microsoft.Extensions.Options;

namespace Njord.Core.Configuration.Trends;

public sealed class TrendOptionsValidator : IValidateOptions<TrendOptions>
{
    // TrendOptions only carries the Enabled toggle, which every value of bool satisfies.
    public ValidateOptionsResult Validate(string? name, TrendOptions options) => ValidateOptionsResult.Success;
}
