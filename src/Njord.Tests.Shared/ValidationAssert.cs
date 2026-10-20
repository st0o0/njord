using Microsoft.Extensions.Options;
using Xunit;

namespace Njord.Tests.Shared;

public static class ValidationAssert
{
    public static string SingleFailure(ValidateOptionsResult result)
    {
        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        return Assert.Single(result.Failures);
    }
}
