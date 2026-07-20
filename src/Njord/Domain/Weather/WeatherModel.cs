using Newtonsoft.Json;

namespace Njord.Domain.Weather;

/// <summary>An Open-Meteo model id (e.g. "icon_d2"). Free-form by design — the API accepts arbitrary strings.</summary>
public sealed record WeatherModel
{
    [JsonProperty("id")]
    public string Id { get; }

    public WeatherModel(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id.Trim();
    }
}
