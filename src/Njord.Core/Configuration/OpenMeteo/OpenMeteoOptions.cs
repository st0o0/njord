namespace Njord.Core.Configuration.OpenMeteo;

public sealed class OpenMeteoOptions
{
    public const string SectionName = "Njord:OpenMeteo";

    public string BaseUrl { get; set; } = "https://api.open-meteo.com/";
}
