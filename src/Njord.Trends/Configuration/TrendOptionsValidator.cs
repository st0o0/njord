using Microsoft.Extensions.Options;

namespace Njord.Trends.Configuration;

public sealed class TrendOptionsValidator : IValidateOptions<TrendOptions>
{
    // TrendOptions only carries the Enabled toggle, which every value of bool satisfies.
    public ValidateOptionsResult Validate(string? name, TrendOptions options) => ValidateOptionsResult.Success;
}
