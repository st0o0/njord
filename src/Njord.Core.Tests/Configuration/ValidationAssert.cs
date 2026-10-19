using Microsoft.Extensions.Options;

namespace Njord.Core.Tests.Configuration;

internal static class ValidationAssert
{
    public static string SingleFailure(ValidateOptionsResult result)
    {
        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        return Assert.Single(result.Failures);
    }
}
