namespace Njord.Domain;

/// <summary>A Kachelmann model id. Free-form by design — the API accepts arbitrary strings.</summary>
public sealed record WeatherModel
{
    public string Id { get; }

    public WeatherModel(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id.Trim();
    }
}
