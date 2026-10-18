using Microsoft.Extensions.Options;

namespace Njord.Core.Configuration.OpenMeteo;

public sealed class OpenMeteoOptionsValidator : IValidateOptions<OpenMeteoOptions>
{
    public ValidateOptionsResult Validate(string? name, OpenMeteoOptions options)
    {
        return Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"Njord:OpenMeteo:BaseUrl must be an absolute http(s) URL, got '{options.BaseUrl}'.");
    }
}
