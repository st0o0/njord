using Njord.Core.Configuration.OpenMeteo;
using Njord.Tests.Shared;

namespace Njord.Core.Tests.Configuration.OpenMeteo;

public sealed class OpenMeteoOptionsValidatorSpec
{
    private static readonly OpenMeteoOptionsValidator Validator = new();

    [Fact]
    public void The_default_endpoint_is_valid()
    {
        Assert.True(Validator.Validate(null, AppSettingsDefaults.Options<OpenMeteoOptions>()).Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("api.open-meteo.com")]
    [InlineData("ftp://api.open-meteo.com/")]
    public void A_non_http_or_relative_url_is_rejected(string url)
    {
        var result = Validator.Validate(null, AppSettingsDefaults.Options<OpenMeteoOptions>(o => o.BaseUrl = url));

        Assert.True(result.Failed);
        Assert.Contains("OpenMeteo:BaseUrl", result.FailureMessage);
    }
}
