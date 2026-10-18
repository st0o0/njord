namespace Njord.Core.Configuration.OpenMeteo;

public sealed class OpenMeteoOptions
{
    public const string SectionName = "Njord";

    public string OpenMeteoBaseUrl { get; set; } = string.Empty;
}
