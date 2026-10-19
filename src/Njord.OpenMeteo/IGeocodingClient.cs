namespace Njord.OpenMeteo;

public interface IGeocodingClient
{
    Task<GeocodingResult?> ResolveAsync(string name, CancellationToken cancellationToken);
}
