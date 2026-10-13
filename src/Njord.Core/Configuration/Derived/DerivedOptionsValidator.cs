using Microsoft.Extensions.Options;

namespace Njord.Core.Configuration.Derived;

public sealed class DerivedOptionsValidator : IValidateOptions<DerivedOptions>
{
    // DerivedOptions only carries the Enabled toggle, which every value of bool satisfies.
    public ValidateOptionsResult Validate(string? name, DerivedOptions options) => ValidateOptionsResult.Success;
}
