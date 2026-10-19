namespace Njord.OpenMeteo;

public sealed class GeocodingClient(HttpClient httpClient) : IGeocodingClient
{
    public async Task<GeocodingResult?> ResolveAsync(string name, CancellationToken cancellationToken)
    {
        var response = await WeatherClient.FetchCoreAsync(
            httpClient, BuildUri(name), OpenMeteoJsonContext.Default.GeocodingResponse, cancellationToken);
        return response?.Results is { Length: > 0 } results ? results[0] : null;
    }

    internal static string BuildUri(string name) =>
        $"v1/search?name={Uri.EscapeDataString(name)}&count=1&language=en&format=json";
}
